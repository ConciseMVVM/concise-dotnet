using System;

namespace Concise.Observables
{
    public class Variable<T>: MutableObservableValue<T>
    {
        private T? _value;
        private T? _futureValue;

        public Variable(T initialValue) =>
            (_value) = (initialValue);

        protected override void SetValueImplementation(T value) =>
            _futureValue = value;

        protected override T GetValueImplementation() =>
            _value;

        protected override bool UpdateValueImplementation() 
        {
            if (Disposed)
                return false;

            if (_futureValue == null)
                return false;

            var futureValue = _futureValue;
            _futureValue = default;

            if (futureValue is IEquatable<T> equatable)
            {
                if (equatable.Equals(_value))
                    return false;
            }

            _value = futureValue;

            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if (!Disposed && disposing)
            {
                _value = default(T);
                _futureValue = default(T);
            }

            base.Dispose(disposing);
        }

    }
}
