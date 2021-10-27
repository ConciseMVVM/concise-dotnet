using System;
using System.Collections;
using System.Collections.Specialized;
using System.Linq;
using Concise.Library;

namespace Concise.Observables
{
    public class FirstOrDefaultObservableValue: ObservableValue<object?>
    {
        private static readonly object None = new();

        private INotifyCollectionChanged _collection;
        private object? _value;
        private object? _futureValue = None;

        private object? CollectionValue()
        {
            var collectionEnumerable = (IEnumerable)_collection;
            var enumerator = collectionEnumerable.GetEnumerator();

            var item = enumerator.MoveNext() ? enumerator.Current : null;

            if (item is IObservableValueConvertable observableValueConvertable)
                item = observableValueConvertable.AsObservableValue();

            if (item == null)
                return item;

            return item;
        }

        public FirstOrDefaultObservableValue(INotifyCollectionChanged collection)
        {
            if (collection is not IEnumerable)
                throw new InvalidOperationException("collction must conform to IEnumerable");
            _collection = collection;

            _value = CollectionValue();

            collection.CollectionChanged += CollectionChanged;
        }

        private void CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // Anytime the collection changes, see if the first element has changed...

            var collectionValue = CollectionValue();
            var pendingValue = (_futureValue == None) ? _value : _futureValue;

            if (collectionValue == pendingValue)
                return; // no change detected.

            _futureValue = collectionValue;
            this.SetNeedsUpdate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _collection.CollectionChanged -= CollectionChanged;
            }

            base.Dispose(disposing);
        }

        protected override object? GetValueImplementation() => _value;

        protected override bool UpdateValueImplementation()
        {
            var futureValue = _futureValue;
            _futureValue = None;

            if (futureValue == None || futureValue == _value)
                return false;

            _value = futureValue;
            return true;
        }
    }

    public static class FirstOrDefaultObservableValueExtensions
    {
        private static WeakExtensionProperty<FirstOrDefaultObservableValue> ObservableValueProperty = new();

        public static T? FirstOrDefaultObservable<T,U>(this IQueryable<U> queryable) where T: class
        {
            if (queryable is not INotifyCollectionChanged || queryable is not IEnumerable)
                throw new InvalidOperationException("Queryable must conform to both INotifyCollectionChanged and IEnumerable");

            var observable = ObservableValueProperty.GetValue(queryable, () => new FirstOrDefaultObservableValue((INotifyCollectionChanged)queryable));

            return observable.Value as T;
        }
    }
}
