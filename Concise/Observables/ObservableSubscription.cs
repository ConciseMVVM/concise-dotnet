using System;
using System.Collections.Generic;
using System.Diagnostics;
using Concise.Library;

namespace Concise.Observables
{
    public interface IObservableSubscription: IDisposable
    {
        string Id { get; }
        IObservableValue? ObservableValue { get; }

        internal ObservableSubscription UnderlyingSubscription { get; }
    }

    internal class ObservableSubscription: IObservableSubscription
    {
        public string Id { get; }
        internal bool IsWeak { get; }
        internal bool IsAlive => _action?.IsAlive ?? false;

        private ObjectReference<Action>? _action;
        private bool _disposed = false;
        private ObservableState? _observableState;
        private string _actionDesc;

        public IObservableValue? ObservableValue => _observableState?.Observable;
        ObservableSubscription IObservableSubscription.UnderlyingSubscription => this;

        static string GetActionDesc(Action action) =>
            $"{action.Target.GetType().Name}.{action.Method}";

        internal ObservableSubscription(ObservableState observableState, string id, Action action, bool isWeak) =>
            (_observableState, Id, IsWeak, _action, _actionDesc) = (observableState, id, isWeak, new(action, isWeak), GetActionDesc(action));

        internal bool Invoke()
        {
            var target = _action?.Target;

            if (target == null)
                return false;

            target.Invoke();

            return true;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                if (_observableState != null)
                    _observableState.Unsubscribe(this);

                _action = null;
                _observableState = null;
            }

            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public override string ToString()
        {
            List<string> args = new();

            if (_disposed)
            {
                args.Add("Disposed");
            }
            else
            {
                if (IsWeak)
                {
                    args.Add("IsWeak");
                    if (!IsAlive)
                        args.Add("IsAlive=false");
                }

            }

            args.Add($"Action={_actionDesc}");

            return $"Subscription#{Id}({string.Join(",", args)})";
        }
    }

    internal class ObservableSubscriptionStrongActionWrapper: IObservableSubscription
    {
        private ObservableSubscription _underlyingSubscription;
        private Action _strongAction;

        internal ObservableSubscriptionStrongActionWrapper(ObservableSubscription underlyingSubscription, Action action) =>
            (_underlyingSubscription, _strongAction) = (underlyingSubscription, action);

        public string Id => _underlyingSubscription.Id;
        public IObservableValue? ObservableValue => _underlyingSubscription.ObservableValue;
        ObservableSubscription IObservableSubscription.UnderlyingSubscription => _underlyingSubscription;
        public void Dispose() => _underlyingSubscription.Dispose();

        public override string ToString() =>
            $"StrongActionWrapper.{_underlyingSubscription}";
    }
}
