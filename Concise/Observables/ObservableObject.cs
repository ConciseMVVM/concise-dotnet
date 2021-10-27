using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Concise.Observables;

namespace Concise.Observables
{
    public class ObservableObject
    {
        private Dictionary<string, IObservableValue> _observableValues = new();
        protected readonly DisposableCollection Subscriptions = new();
        public bool WeakSubscriptions { get; protected set; }

        private bool _addedObservableValues = false;

        protected virtual void AddObservableValues()
        {
        }

        private void AddObservableValuesIfNeeded()
        {
            if (_addedObservableValues)
                return;

            _addedObservableValues = true;

            AddObservableValues();
        }

        protected virtual void OnObservableValueAdded(string propertyName, IObservableValue observableValue)
        {
        }

        protected IObservableValue AddObservableValue(string propertyName, IObservableValue observableValue)
        {
            _observableValues[propertyName] = observableValue;

            observableValue.SetTag($"{GetType().Name}.{propertyName}");

            Debug.WriteLine($"{this.GetType().Name}: Adding property {observableValue.GetDebugDescription()}");

            OnObservableValueAdded(propertyName, observableValue);

            return observableValue;
        }

        protected Variable<T> AddVariable<T>(string propertyName, T defaultValue)
        {
            var variable = new Variable<T>(defaultValue);

            if (WeakSubscriptions)
                variable.WeakSubscriptions = true;

            AddObservableValue(propertyName, variable);

            return variable;
        }

        protected Expression<T> AddExpression<T>(string propertName, Func<T> func)
        {
            var expression = new Expression<T>(func);

            if (WeakSubscriptions)
                expression.WeakSubscriptions = true;

            AddObservableValue(propertName, expression);

            return expression;
        }

        protected T GetValue<T>([CallerMemberName] string propertyName = "")
        {
            AddObservableValuesIfNeeded();

            if (!_observableValues.TryGetValue(propertyName, out var observableValue))
                throw new InvalidOperationException($"invalid property name {propertyName}");

            var v = observableValue as IObservableValue<T> ?? throw new InvalidCastException("property is not correct type");

            return v.GetValue();
        }

        protected void SetValue<T>(T value, [CallerMemberName] string propertyName = "")
        {
            AddObservableValuesIfNeeded();
            if (!_observableValues.TryGetValue(propertyName, out var observableValue))
                throw new InvalidOperationException($"invalid property name {propertyName}");

            var v = observableValue as MutableObservableValue<T> ?? throw new InvalidCastException("property is not settable");

            v.Value = value;
        }

        public IObservableValue this[string propertyName]
        {
            get
            {
                AddObservableValuesIfNeeded();

                if (!_observableValues.TryGetValue(propertyName, out var observableValue))
                    throw new IndexOutOfRangeException();

                return observableValue;
            }
        }

        public IReadOnlyDictionary<string, IObservableValue> ObservableValues
        {
            get
            {
                AddObservableValuesIfNeeded();
                return new ReadOnlyDictionary<string, IObservableValue>(_observableValues);
            }
        }
    }
}
