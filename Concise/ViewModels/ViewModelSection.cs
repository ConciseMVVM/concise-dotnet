using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Concise.Observables;

namespace Concise.ViewModels
{
    public class ViewModelSection<T> : ViewModelItem, IObservableList<T> where T : ViewModelItem
    {
        public IReadOnlyList<T> Items { get; }

        #region INotifyCollectionChanged implementation

        NotifyCollectionChangedEventHandler? _collectionChanged;

        event NotifyCollectionChangedEventHandler INotifyCollectionChanged.CollectionChanged
        {
            add => _collectionChanged += value;
            remove => _collectionChanged -= value;
        }

        protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs e) =>
            _collectionChanged?.Invoke(this, e);

        #endregion

        #region IObservableList implementations

        public T this[int index] => Items[index];

        bool ICollection.IsSynchronized => false;
        object ICollection.SyncRoot => Items;
        void ICollection.CopyTo(Array array, int index)
        {
            // todo: Support CopyTo
            throw new InvalidOperationException();
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => Items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => Items.GetEnumerator();

        #endregion

        /// <summary>
        /// Count of items. ObservableValue.
        /// </summary>
        public int Count
        {
            get => GetValue<int>();
            private set => SetValue(value);
        }

        public ViewModelSection(IReadOnlyList<T> items)
        {
            Items = items;

            if (Items is INotifyCollectionChanged collectionChanged)
                collectionChanged.CollectionChanged += Items_CollectionChanged;

            AddVariable(nameof(Count), Items.Count);
        }

        public ViewModelSection(IList<T> items) :
            this((items as IReadOnlyList<T>) ?? (IReadOnlyList<T>)new ReadOnlyCollection<T>(items))
        {
        }

        private void Items_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            Count = Items.Count; // update our observable count if has changed.
            OnCollectionChanged(e);
        }
    }

}
