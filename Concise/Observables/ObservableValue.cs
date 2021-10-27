using System;

namespace Concise.Observables
{
    public abstract class ObservableValue : IObservableValue
    {
        ObservableState? IObservableValue.ObservableState { get; set; }
        public string Id => this.GetId();
        public ObservableDomain Domain => this.GetDomain();
        public bool NeedsUpdate => this.GetNeedsUpdate();
        protected bool Disposed { get; private set; }

        public bool WeakSubscriptions
        {
            get => this.GetWeakSubscriptions();
            set => this.SetWeakSubscriptions(value);
        }

        public string? Tag
        {
            get => this.GetTag();
            set => this.SetTag(value);
        }

        bool IObservableValue.UpdateValueImplementation() =>
            UpdateValueImplementation();

        protected abstract bool UpdateValueImplementation();

        public IObservableValue AsObservableValue() => this;

        public ObservableValue()
        {
            this.InitializeObservableValue();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (Disposed)
                return;

            Disposed = true;

            if (disposing)
            {
                var i = (IObservableValue)this;

                if (i.ObservableState != null)
                {
                    i.ObservableState.Dispose();
                    i.ObservableState = null;
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public string DebugDescription => this.GetDebugDescription();

        public override string ToString() => DebugDescription;
    }


    public abstract class ObservableValue<T> : ObservableValue, IObservableValue<T>
    {
        T IObservableValue<T>.GetValueImplementation() =>
            GetValueImplementation();

        protected abstract T GetValueImplementation();

        public T Value => this.GetValue();

        public ObservableValue() :
            base()
        {
        }
    }

    public abstract class MutableObservableValue<T>: ObservableValue<T>, IMutableObservableValue<T>
    {
        void IMutableObservableValue<T>.SetValueImplementation(T value) =>
            SetValueImplementation(value);

        protected abstract void SetValueImplementation(T value);

        public new T Value
        {
            get => this.GetValue();
            set => this.SetValue(value);
        }

        public MutableObservableValue() :
            base()
        {
        }
    }
}
