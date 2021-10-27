using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Concise.Observables
{
    public class ObservableValueWrapper: IObservableValue, INotifyPropertyChanged, IDisposable
    {
        static readonly object Null = new();

        public ObservableState? ObservableState { get; set; }

        public INotifyPropertyChanged? Target { get; private set; }

        Dictionary<string, PropertyInfo> _properties = new();
        Dictionary<string, object?> _values = new();
        Dictionary<string, object?> _futureValues = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new(propertyName));

        private static IEnumerable<PropertyInfo> GetAllProperties(Type t)
        {
            foreach (var property in t.GetProperties())
                yield return property;

            if (t.IsInterface)
            {
                foreach (var i in t.GetInterfaces())
                {
                    foreach (var property in GetAllProperties(i))
                        yield return property;
                }
            }
        }

        public ObservableValueWrapper(INotifyPropertyChanged target, Type t)
        {
            Target = target;
            this.InitializeObservableValue();

            // Reflect our properties...

            foreach (var propertyInfo in GetAllProperties(t))
                _properties[propertyInfo.Name] = propertyInfo;

            // Load our initial values from the target...

            foreach (var propertyInfo in _properties.Values)
                _values[propertyInfo.Name] = propertyInfo.GetValue(target);

            // Start watching for changes...

            target.PropertyChanged += Target_PropertyChanged;
        }


        private void Target_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_disposed)
                return;

            if (!_properties.TryGetValue(e.PropertyName, out var propertyInfo))
                return; // ignore

            // Grab the changed value right away.

            _futureValues[propertyInfo.Name] = propertyInfo.GetValue(Target);

            // Let Concise know we have a change...

            this.SetNeedsUpdate();
        }

        public bool UpdateValueImplementation()
        {
            if (_disposed)
                return false;

            bool changed = false;

            foreach (var pair in _futureValues)
            {
                if (_values[pair.Key] == pair.Value)
                    continue; // value didn't actually change, ignore

                _values[pair.Key] = pair.Value;
                OnPropertyChanged(pair.Key);

                changed = true;
            }

            // Reset futureValues for the next batch of changes...

            _futureValues.Clear();

            return changed;
        }

        public T GetValue<T>([CallerMemberName] string propertyName = "")
        {
            if (_disposed)
                throw new ObjectDisposedException("ObservableValueWrapper");

            if (!_values.TryGetValue(propertyName, out var value))
                throw new InvalidOperationException($"Invalid property: {propertyName}");

            // Let Concise know we are reading a value
            // this allows us to capture this object in expressions.

            this.WillReadValue();

            return (T)value;
        }

        public void SetValue<T>(T value, [CallerMemberName] string propertyName = "")
        {
            // We will set the value on the underlying object, but not change ourc
            // urrent values. This maintains our "snapshot" isolation but it means
            // that if you set a value you won't see the value change
            // immediately in the wrapper.

            if (_disposed)
                throw new ObjectDisposedException("ObservableValueWrapper");

            if (!_properties.TryGetValue(propertyName, out var propertyInfo))
                throw new InvalidOperationException($"Invalid property: {propertyName}");

            propertyInfo.SetValue(Target, value);

            // We are depending on the Target to call INotifyPropertyChanged here...
        }

        public IObservableValue AsObservableValue() => this;

        #region Dispose

        private bool _disposed = false;

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                if (Target != null)
                {
                    Target.PropertyChanged -= Target_PropertyChanged;
                    Target = null;
                }
            }

            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
