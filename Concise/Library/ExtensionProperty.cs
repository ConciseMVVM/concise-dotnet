using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace Concise.Library
{
    public class ExtensionProperty<T>
    {
        private class ValueWrapper
        {
            public long ObjectId { get; }
            private WeakReference ObjectRef { get; }
            public bool IsAlive => ObjectRef.IsAlive;
            private T _value;

            public ValueWrapper(long objectId, object obj, T value)
            {
                ObjectId = objectId;
                ObjectRef = new WeakReference(obj);
                _value = value;
            }

            public void SetValue(T value)
            {
                if (!IsAlive)
                    throw new InvalidOperationException("Object unexpectedly deallocated");

                _value = value;
            }

            public T GetValue()
            {
                if (!IsAlive)
                    throw new InvalidOperationException("Object unexpectedly deallocated");

                return _value;
            }
        }

        private ObjectIDGenerator _objectIDGenerator = new();
        private Dictionary<long, ValueWrapper> _values = new();
        private Func<T> DefaultValue { get; }

#pragma warning disable CS8601 // Possible null reference assignment.
        public ExtensionProperty(T defaultValue = default) =>
#pragma warning restore CS8601 // Possible null reference assignment.
            (DefaultValue) = (() => defaultValue);

        public ExtensionProperty(Func<T> defaultValue) =>
            (DefaultValue) = (defaultValue);

        private void Cleanup()
        {
            var deadObjectIds = _values.Values.Where((v) => !v.IsAlive).Select((v) => v.ObjectId);

            foreach (var objectId in deadObjectIds)
                _values.Remove(objectId);
        }

        private ValueWrapper GetValueWrapper(object obj, Func<T> initialValue)
        {
            Cleanup();

            var objectId = _objectIDGenerator.GetId(obj, out var firstTime);

            if (firstTime)
            {
                var newValue = new ValueWrapper(objectId, obj, initialValue.Invoke());
                _values.Add(objectId, newValue);

                return newValue;
            }

            if (!_values.TryGetValue(objectId, out var value))
                throw new InvalidOperationException("Value not found when when it should already exist");

            return value;
        }

        public void RemoveValue(object obj)
        {
            var objectId = _objectIDGenerator.GetId(obj, out var firstTime);

            if (firstTime)
                return;

            _values.Remove(objectId);
        }

        public T GetValue(object obj, Func<T>? initialValue = null) =>
            GetValueWrapper(obj, initialValue ?? DefaultValue).GetValue();

        public void SetValue(object obj, T value) =>
            GetValueWrapper(obj, () => value).SetValue(value);

        public T CacheValue(object obj, Func<T> func) =>
            GetValue(obj, func);
    }


    public class WeakExtensionProperty<T> where T: class
    {
        private ExtensionProperty<WeakReference> _prop;

#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        public WeakExtensionProperty(T defaultValue = default) =>
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
            _prop = new(() => new WeakReference(defaultValue));

        public T GetValue(object obj, Func<T>? initialValue = null)
        {
            Func<WeakReference> initialValueRef = () => initialValue != null ? new WeakReference(initialValue()) : new WeakReference(default(T));

            var weak = _prop.GetValue(obj, initialValueRef);

            if (!weak.IsAlive)
                _prop.SetValue(obj, initialValueRef());

            return (T)weak.Target;
        }

        public void SetValue(object obj, T value)
        {
            _prop.SetValue(obj, new WeakReference(value));
        }

        public void RemoveValue(object obj) =>
            _prop.RemoveValue(obj);

        public T CacheValue(object obj, Func<T> func)
        {
            var cached = GetValue(obj);

            if (cached != null)
                return cached;

            var value = func();

            SetValue(obj, value);

            return value;
        }
    }
}
