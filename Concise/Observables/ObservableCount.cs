using System;
using System.Collections;
using System.Collections.Specialized;
using System.Linq;
using Concise.Library;

namespace Concise.Observables
{
    public class ObservableCountValue : ObservableValue<int>
    {
        private INotifyCollectionChanged _collection;
        private int _value;
        private int? _futureValue = null;

        private int GetCount() => ((ICollection)_collection).Count;

        public ObservableCountValue(INotifyCollectionChanged collection)
        {
            if (collection is not ICollection)
                throw new InvalidOperationException("collection must implement ICollection");

            _collection = collection;

            _value = GetCount();

            collection.CollectionChanged += CollectionChanged;
        }

        private void CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // Anytime the collection changes, see if the count has changed...

            var newValue = GetCount();
            var pendingValue = _futureValue ?? _value;

            if (newValue == pendingValue)
                return; // no change detected.

            _futureValue = newValue;
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

        protected override int GetValueImplementation() => _value;

        protected override bool UpdateValueImplementation()
        {
            var futureValue = _futureValue;
            _futureValue = null;

            if (futureValue == null || futureValue == _value)
                return false;

            _value = futureValue.Value;

            return true;
        }

    }

    public static class ObservableCountValueExtensions
    {
        private static readonly WeakExtensionProperty<ObservableCountValue> ObservableCountValueProperty = new();

        private static int GetObservableCount(object collection)
        {
            if (collection is not INotifyCollectionChanged)
                throw new InvalidOperationException("collection must implement INotifyCollectionChanged");

            var observable = ObservableCountValueProperty.GetValue(collection, () => new ObservableCountValue((INotifyCollectionChanged)collection));

            return observable.Value;
        }

        public static int ObservableCount<U>(this IQueryable<U> queryable) => GetObservableCount(queryable);
        public static int ObservableCount(this INotifyCollectionChanged collection) => GetObservableCount(collection);
    }
}
