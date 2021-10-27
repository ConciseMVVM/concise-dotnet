using System;
using System.Threading.Tasks;

namespace Concise.Observables
{
    public interface IObservableValue: IDisposable, IObservableValueConvertable
    {
        ObservableState? ObservableState { get; set; }
        bool UpdateValueImplementation();
    }

    public interface IObservableValueConvertable
    {
        IObservableValue AsObservableValue();
    }

    public interface IObservableValue<T> : IObservableValue
    {
        T GetValueImplementation();
    }

    public interface IMutableObservableValue<T> : IObservableValue<T>
    {
        void SetValueImplementation(T value);
    }

    public static class IObservableValueExtensions
    {
        public static IObservableValue AsObservableValue(this IObservableValue observableValue) => observableValue;

        public static void InitializeObservableValue(this IObservableValue observableValue, ObservableDomain? domain=null)
        {
            if (observableValue.ObservableState != null)
                throw new NotSupportedException("InitializeObservableValue may only be called once for each ObservableValue");

            domain ??= ObservableDomain.Current;

            if (domain == null)
                throw new Exception("Invalid Thread - not associated with ObservableDomain");

            observableValue.ObservableState = new ObservableState(domain, observableValue);
        }

        public static T WithState<T>(this IObservableValue observableValue, Func<ObservableState, T> func)
        {
            if (observableValue.ObservableState == null)
                throw new Exception("InitializeObservableValue was not called for this ObservableValue");

            observableValue.ObservableState.Domain.EnsureCurrentContext();

            return func(observableValue.ObservableState);
        }

        public static void WithState(this IObservableValue observableValue, Action<ObservableState> action) =>
            observableValue.WithState<object?>((s) =>
            {
                action(s);
                return null;
            });

        public static T WithCrossDomainSafeState<T>(this IObservableValue observableValue, Func<ObservableState, T> func)
        {
            if (observableValue.ObservableState == null)
                throw new Exception("InitializeObservableValue was not called for this ObservableValue");

            observableValue.ObservableState.Domain.EnsureCurrentContext();

            return func(observableValue.ObservableState);
        }

        public static void WithCrossDomainSafeState(this IObservableValue observableValue, Action<ObservableState> action) =>
            observableValue.WithCrossDomainSafeState<object?>((s) =>
            {
                action(s);
                return null;
            });

        public static void SetNeedsUpdate(this IObservableValue observableValue) =>
            observableValue.WithState((s) => s.Domain.ObservableNeedsUpdate(s));

        public static void WillReadValue(this IObservableValue observableValue) =>
            observableValue.WithState((s) => s.Domain.ObservableWillReadValue(s));

        public static string GetId(this IObservableValue observableValue) =>
            observableValue.WithCrossDomainSafeState((s) => s.Id);

        public static string? GetTag(this IObservableValue observableValue) =>
            observableValue.WithCrossDomainSafeState((s) => s.Tag);

        public static void SetTag(this IObservableValue observableValue, string? tag) =>
            observableValue.WithState((s) => s.Tag = tag);

        public static string GetDebugDescription(this IObservableValue observableValue) =>
            observableValue.WithCrossDomainSafeState((s) => s.ToString());

        public static ObservableDomain GetDomain(this IObservableValue observableValue) =>
            observableValue.WithCrossDomainSafeState((s) => s.Domain);

        public static bool GetNeedsUpdate(this IObservableValue observableValue) =>
            observableValue.WithState((s) => s.NeedsUpdate);

        public static bool GetWeakSubscriptions(this IObservableValue observableValue) =>
            observableValue.WithState((s) => s.WeakSubscriptions);

        public static void SetWeakSubscriptions(this IObservableValue observableValue, bool value) =>
            observableValue.WithState((s) => s.WeakSubscriptions = value);

        public static void AddDependency(this IObservableValue observableValue, IObservableValue dependency) =>
            observableValue.WithState((s) => dependency.WithState((dep) => s.AddDependency(dep)));

        public static bool RemoveDependency(this IObservableValue observableValue, IObservableValue dependency) =>
            observableValue.WithState((s) => dependency.WithState((dep) => s.RemoveDependency(dep)));

        public static IObservableSubscription Subscribe(this IObservableValue observableValue, Action action) =>
            observableValue.WithState((s) => s.Subscribe(action));

        public static IObservableSubscription SubscribeStrong(this IObservableValue observableValue, Action action) =>
            observableValue.WithState((s) => s.Subscribe(action, false));

        public static IObservableSubscription SubscribeWeak(this IObservableValue observableValue, Action action) =>
            observableValue.WithState((s) => s.Subscribe(action, true));

        public static void Unsubscribe(this IObservableValue observableValue, IObservableSubscription subscription) =>
            observableValue.WithState((s) => s.Unsubscribe(subscription));

        public static bool UpdateValueIfNeeded(this IObservableValue observableValue)
        {
            var state = observableValue.ObservableState;

            if (state == null || state.Disposed || state.Domain == null)
                throw new ObjectDisposedException("ObservableState");

            state.Domain.EnsureCurrentContext();

            // ensure we have the current value. Requires updating all currently
            // dirty values in the domain.

            if (state.NeedsUpdate)
            {
                state.Domain.UpdateValuesIfNeeded();
                return true;
            }
            else
            {
                return false;
            }
        }

        public static T GetValue<T>(this IObservableValue<T> observableValue)
        {
            observableValue.UpdateValueIfNeeded();
            observableValue.WillReadValue();
            return observableValue.GetValueImplementation();
        }

        public static void SetValue<T>(this IMutableObservableValue<T> mutableObservableValue, T value)
        {
            var state = mutableObservableValue.ObservableState;

            if (state == null || state.Disposed || state.Domain == null)
                throw new ObjectDisposedException("ObservableState");

            state.Domain.EnsureCurrentContext();
            state.Domain.ObservableNeedsUpdate(state);
            mutableObservableValue.SetValueImplementation(value);
        }
      
        //public static Task<T> GetLastestValueAsync<T>(this IObservableValue<T> observableValue)
        //{
        //    // thread-safe & callable from non-Domain threads...

        //    if (observableValue.GetDomain() == ObservableDomain.Current && !observableValue.GetNeedsUpdate())
        //        return Task.FromResult(observableValue.GetValueImplementation());

        //    TaskCompletionSource<T> taskCompletionSource = new();

        //    // wait until updates have been posted then return the updated value...
        //    observableValue.GetDomain().Enqueue(() => taskCompletionSource.SetResult(observableValue.GetValueImplementation()));

        //    return taskCompletionSource.Task;
        //}
    }
}
