using System;
using System.Collections.Generic;
using System.Linq;

namespace Concise.Observables
{
    public class Expression<T>: ObservableValue<T>
    {
        private Func<T>? _func;
        private T? _value;
        private IEnumerable<IObservableValue>? _dependents;

        private static (T, IEnumerable<IObservableValue>) Exec(ObservableDomain domain, Func<T> func)
        {
            T value = default;
            var dependents = domain.CaptureReads(() => value = func());

#pragma warning disable CS8619 // Nullability of reference types in value doesn't match target type.
            return (value, dependents);
#pragma warning restore CS8619 // Nullability of reference types in value doesn't match target type.
        }

        public Expression(Func<T> func)
        {
            _func = func;
            (_value, _dependents) = Exec(Domain, func);

            foreach(var dependent in _dependents)
                dependent.AddDependency(this);
        }

        protected override void Dispose(bool disposing)
        {
            if (!Disposed && disposing)
            {
                _func = null;
                _value = default(T);
                _dependents = null;
            }

            base.Dispose(disposing);

        }

        protected override T GetValueImplementation()
        {
            if (Disposed)
                throw new ObjectDisposedException("Expression");

            return _value;
        }

        protected override bool UpdateValueImplementation()
        {
            if (Disposed)
                return false;

            // execute our function...

            (var newValue, var newDependents) = Exec(Domain, _func);

            // update dependencies...

            var existing = _dependents.ToDictionary((v) => v.GetId());

            foreach(var dependent in newDependents)
            {
                var id = dependent.GetId();

                if (!existing.Remove(id))
                    dependent.AddDependency(this);
            }

            foreach (var dependent in existing.Values)
                dependent.RemoveDependency(this);

            _dependents = newDependents;

            // If values are equal we are done...

            if (Equals(_value, newValue))
                return false;

            // store our new values and let the system know the value changed...

            _value = newValue;

            return true;
        }
    }
}
