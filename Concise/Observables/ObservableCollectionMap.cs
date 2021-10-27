using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;

namespace Concise.Observables
{
    public class ObservableCollectionMap<S, T> : IObservableList<T>
    {
        public INotifyCollectionChanged Source { get; }

        private Func<S, T> _mapFunc;
        private List<T> _items;

        public event PropertyChangedEventHandler? PropertyChanged;

        private NotifyCollectionChangedEventHandler? _collectionChanged;
        public event NotifyCollectionChangedEventHandler? CollectionChanged
        {
            add
            {
                // If we have any items, send an add event so the event handler is synced...

                //if (value != null && _items.Count > 0)
                //    value(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, _items, 0));

                _collectionChanged += value;
            }
            remove => _collectionChanged -= value;
        }

        protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs e) =>
            _collectionChanged?.Invoke(this, e);

        protected virtual void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private IList<T> MapItems(IEnumerable items)
        {
            List<T> newItems = new();

            foreach (object item in items)
            {
                if (item is S sourceItem)
                    newItems.Add(_mapFunc(sourceItem));
                else
                    throw new NotSupportedException("Item is not a Source type instance");
            }

            return newItems;
        }

        public ObservableCollectionMap(INotifyCollectionChanged source, Func<S,T> mapFunc)
        {
            Source = source;
            _mapFunc = mapFunc;

            Source.CollectionChanged += Source_CollectionChanged;
            _items = new();

            // populate initial items if we have access to them...

            if (source is IEnumerable<S> enumerable)
                _items.AddRange(MapItems(enumerable));
        }

        ~ObservableCollectionMap()
        {
            Source.CollectionChanged -= Source_CollectionChanged;
        }

        private NotifyCollectionChangedEventArgs AddItems(int startingIndex, IEnumerable items)
        {
            var newItems = MapItems(items);
            _items.InsertRange(startingIndex, newItems);

            return new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, (IList)newItems, startingIndex);
        }

        private NotifyCollectionChangedEventArgs RemoveItems(int startingIndex, int count)
        {
            var oldItems = _items.GetRange(startingIndex, count);
            _items.RemoveRange(startingIndex, count);

            return new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, (IList)oldItems, startingIndex);
        }

        private NotifyCollectionChangedEventArgs ReplaceItems(int startingIndex, IEnumerable items, int oldItemCount)
        {
            var newItems = MapItems(items);
            var oldItems = _items.GetRange(startingIndex, oldItemCount);
            _items.RemoveRange(startingIndex, oldItemCount);
            _items.InsertRange(startingIndex, newItems);

            return new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, (IList)newItems, (IList)oldItems, startingIndex);
        }

        private NotifyCollectionChangedEventArgs MoveItems(int startingIndex, IEnumerable items, int oldStartingIndex)
        {
            var newItems = MapItems(items);

            // will this work in all cases? (ie is oldStartingIndex always correct)

            _items.RemoveRange(oldStartingIndex, newItems.Count);
            _items.InsertRange(startingIndex, newItems);

            return new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move, (IList)newItems, startingIndex, oldStartingIndex);
        }

        private NotifyCollectionChangedEventArgs ResetItems()
        {
            _items.Clear();

            return new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset);
        }

        private static string ArgsToString(NotifyCollectionChangedEventArgs args) =>
            $"{args.Action}(OldStartingIndex={args.OldStartingIndex}, OldItems.Count={args.OldItems?.Count.ToString() ?? "(null)"}, NewStartingIndex={args.NewStartingIndex}, NewItems.Count={args.NewItems?.Count.ToString() ?? "(null)"})";

        private void Source_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            var oldCount = _items.Count;

            NotifyCollectionChangedEventArgs eventArgs = e.Action switch
            {
                NotifyCollectionChangedAction.Add => AddItems(e.NewStartingIndex, e.NewItems),
                NotifyCollectionChangedAction.Remove => RemoveItems(e.OldStartingIndex, e.OldItems.Count),
                NotifyCollectionChangedAction.Replace => ReplaceItems(e.NewStartingIndex, e.NewItems, e.OldItems.Count),
                NotifyCollectionChangedAction.Move => MoveItems(e.NewStartingIndex, e.NewItems, e.OldStartingIndex),
                NotifyCollectionChangedAction.Reset => ResetItems(),
                _ => throw new Exception("Unexpected NotifyCollectionChangedAction")
            };

            if (oldCount != _items.Count)
                OnPropertyChanged(nameof(Count));

            Debug.WriteLine($"ObservableCollectionMap: {ArgsToString(e)} => {ArgsToString(eventArgs)}");

            OnCollectionChanged(eventArgs);
        }

        #region IReadOnlyList/ICollection

        public int Count => _items.Count;
        public bool IsReadOnly => true;
        public bool IsSynchronized => false;
        public object SyncRoot => this;
        public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();
        public void CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);

        public T this[int index]
        {
            get => _items[index];
            set => throw new NotImplementedException();
        }

        #endregion
    }

    public static class INotifyCollectionChangedExtensions
    {
        public static IObservableList<T> Map<S, T>(this INotifyCollectionChanged source, Func<S, T> mapFunc) =>
            new ObservableCollectionMap<S,T>(source, mapFunc);
    }

    public static class IQueryableExtensions
    {
        public static IObservableList<T> MapObservableCollection<S, T>(this IQueryable<S> source, Func<S, T> mapFunc)
        {
            if (source is not INotifyCollectionChanged notifyCollectionChanged)
                throw new InvalidOperationException("IQueryable must implement INotifyCollectionChanged to support MapObservableCollection()");

            return new ObservableCollectionMap<S, T>(notifyCollectionChanged, mapFunc);
        }
    }
}

