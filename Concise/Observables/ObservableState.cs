using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Threading;

namespace Concise.Observables
{
    public class ObservableState : IDisposable
    {
        internal string Id { get; }
        internal ObservableDomain? Domain { get; private set; }
        internal IObservableValue? Observable { get; private set; }

        internal bool NeedsUpdate { get; set; }
        internal List<WeakReference<ObservableState>>? Dependencies { get; set; }
        private List<ObservableSubscription>? _subscriptions = null;
        internal bool Disposed { get; private set; }
        internal bool WeakSubscriptions { get; set; }
        internal string? Tag { get; set; }

        private static Int64 NextObjectId = 0;
        private Int64 NextSubscriptionId = 0;

        internal ObservableState(ObservableDomain domain, IObservableValue observable)
        {
            var id = Interlocked.Increment(ref NextObjectId);
            Id = id.ToString("X4");
            Domain = domain;
            Observable = observable;
        }

        internal void AddDependency(ObservableState observableState)
        {
            if (Disposed)
                throw new ObjectDisposedException("ObservableState");

            Dependencies ??= new(); // lazily create Dependencies list when first used
            Dependencies.Add(new WeakReference<ObservableState>(observableState));
        }

        internal bool RemoveDependency(ObservableState observableState)
        {
            if (Dependencies == null)
                return false;

            var index = Dependencies.FindIndex((weak) => weak.TryGetTarget(out var item) && observableState == item);

            if (index != -1)
            {
                Dependencies.RemoveAt(index);
                return true;
            }

            return false;
        }

        internal IObservableSubscription Subscribe(Action action, bool? isWeak = null)
        {
            if (Disposed)
                throw new ObjectDisposedException("ObservableState");

            _subscriptions ??= new();

            var id = Interlocked.Increment(ref NextSubscriptionId);

            var subscription = new ObservableSubscription(this, $"{Id}.{id}", action, isWeak ?? WeakSubscriptions);

            Debug.WriteLine($"{this}: Adding Subscription: {subscription}");

            _subscriptions.Add(subscription);

            return (subscription.IsWeak) ? new ObservableSubscriptionStrongActionWrapper(subscription, action) : subscription;
        }

        internal void Unsubscribe(IObservableSubscription subscription)
        {
            Debug.WriteLine($"{this}: Removing Subscription: {subscription}");

            if (_subscriptions == null)
                return;

            _subscriptions.Remove(subscription.UnderlyingSubscription);
        }

        internal void InvokeSubscriptions()
        {
            if (_subscriptions == null)
                return;

            foreach (var subscription in _subscriptions)
            {
                if (!subscription.Invoke())
                    Debug.WriteLine($"{this}: Unable to invoke subscription {subscription}");
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (Disposed)
                return;

            Disposed = true;

            if (disposing)
            {
                Domain = null;
                Dependencies = null;

                if (_subscriptions != null)
                {
                    var subscriptions = _subscriptions;
                    _subscriptions = null;

                    foreach (var subscription in subscriptions)
                        subscription.Dispose();
                }

                if (Observable != null)
                {
                    Observable.Dispose();
                    Observable = null;
                }
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public static string GetFriendlyName(Type type)
        {
            string friendlyName = type.Name;
            if (type.IsGenericType)
            {
                int iBacktick = friendlyName.IndexOf('`');
                if (iBacktick > 0)
                {
                    friendlyName = friendlyName.Remove(iBacktick);
                }
                friendlyName += "<";
                Type[] typeParameters = type.GetGenericArguments();
                for (int i = 0; i < typeParameters.Length; ++i)
                {
                    string typeParamName = GetFriendlyName(typeParameters[i]);
                    friendlyName += (i == 0 ? typeParamName : "," + typeParamName);
                }
                friendlyName += ">";
            }

            return friendlyName;
        }

        public override string ToString()
        {
            var type = Observable?.GetType().GetFriendlyName() ?? "ObservableObject";
            var tag = Tag ?? "";

            return $"{type}#{Id}({tag})";
        }
    }

    public static class TypeNameExtensions
    {
        public static string GetFriendlyName(this Type type)
        {
            string friendlyName = type.Name;
            if (type.IsGenericType)
            {
                int iBacktick = friendlyName.IndexOf('`');
                if (iBacktick > 0)
                {
                    friendlyName = friendlyName.Remove(iBacktick);
                }
                friendlyName += "<";
                Type[] typeParameters = type.GetGenericArguments();
                for (int i = 0; i < typeParameters.Length; ++i)
                {
                    string typeParamName = GetFriendlyName(typeParameters[i]);
                    friendlyName += (i == 0 ? typeParamName : "," + typeParamName);
                }
                friendlyName += ">";
            }

            return friendlyName;
        }

    }
}
