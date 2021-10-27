using System;
using System.Threading.Tasks;

namespace Concise.Observables
{
    public class ValueProxy<T>: ObservableValue<T>
    {
        // this is still a bit of a work in progress...

        public ObservableDomain TargetDomain { get; }
        public IObservableValue<T>? TargetValue { get; private set; } = null;
        private T _value;
        private T _futureValue;
        private IObservableSubscription? _subscription;

        private void ObservableValueChanged()
        {
            // this is called in the TargetDomain thread...

            if (TargetValue == null)
                return;

            _futureValue = TargetValue.GetValue();
            Domain.Enqueue(() => this.SetNeedsUpdate());
        }

        private ValueProxy(ObservableDomain targetDomain, Func<IObservableValue<T>> createValue, T initialValue)
        {
            // create a proxy in this domain for a value in another domain.
            TargetDomain = targetDomain;
            _value = initialValue;
            _futureValue = initialValue;

            targetDomain.Enqueue(() =>
            {
                TargetValue = createValue();
                ObservableValueChanged(); // get initial value
                _subscription = TargetValue.Subscribe(ObservableValueChanged);
            });
        }

        //private ValueProxy(ObservableDomain proxyDomain, IObservableValue targetObservableValue)
        //{
        //    // to do: create a proxy on another domain for a value in this domain

        //}

        public static ValueProxy<T> FromDomain(ObservableDomain targetDomain, Func<IObservableValue<T>> createValue, T initialValue)
            => new ValueProxy<T>(targetDomain, createValue, initialValue);

        //public static ValueProxy<T> FromDomain(IObservableValue<T> observableValue, T initialValue) =>
        //    new ValueProxy<T>(observableValue.GetDomain(), () => observableValue, initialValue)

        ~ValueProxy()
        {
            TargetDomain.Enqueue(() =>
            {
                _subscription?.Dispose();
                TargetValue = null;
            });
        }

        protected override T GetValueImplementation() =>
            _value;

        protected override bool UpdateValueImplementation()
        {
            var futureValue = _futureValue;

            if (Object.Equals(_value, futureValue))
                return false;

            _value = futureValue;

            return true;
        }
    }
}
