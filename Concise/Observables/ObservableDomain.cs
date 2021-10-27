using System;
using System.Collections.Generic;
using System.Threading;
using Concise.Library;

namespace Concise.Observables
{
    public class ObservableDomain
    {
        private List<ObservableState> _dirtyObservables = new();
        private Dictionary<string, ObservableState> _changedObservables = new();

        private Queue<Action> _tasks = new Queue<Action>();

        public bool NeedsUpdate { get; private set; }
        public SynchronizationContext? Context { get; private set; } = null;

        static readonly ThreadLocal<ObservableDomain?> _current = new();

        public static ObservableDomain? Current
        {
            get => _current.Value;
            set => _current.Value = value;
        }

        public void SetContext(SynchronizationContext context)
        {
            if (Context != null)
                throw new Exception("SynchronizationContext may only be set once");

            Context = context;

            if (context == SynchronizationContext.Current)
                Current = this;
            else
                context.Send((_) => Current = this, null);

            if (_tasks.Count > 0)
                SetNeedsUpdate(); // we can now perform any queued tasks
        }

        public ObservableDomain(SynchronizationContext? context = null)
        {
            if (context != null)
                SetContext(context);
        }

        public static ObservableDomain ViewDomain = new ObservableDomain();
        public static ObservableDomain ModelDomain = new ObservableDomain();

        public static void Initialize()
        {
            // must be called from the main thread!!!!

            ViewDomain.SetContext(SynchronizationContext.Current);
            ModelDomain.SetContext(new SerialSynchronizationContext());
        }

        internal void EnsureCurrentContext()
        {
            if (SynchronizationContext.Current != Context)
                throw new Exception("Invalid Thread - Attempt to access ObservableDomain or ObservableValue from a SynchronizationContext that doesn't own it.");
        }

        internal bool UpdateValuesIfNeeded()
        {
            // Update any "dirty" observables. New dirty observables may
            // be added during this process. We are done when we have processed
            // them all. Any changed objects are added to the _changedObservables
            // dictionary. Subscriptions for those are invoked during the standard Performpdates
            // cycle.
            // This may be invoked multiple times during a run loop -- anytime
            // an attempt to read a value that "NeedsUpdate" this process runs
            // updating all currently dirtyObservables.
            //
            // This method may also be re-entrant in the case the UpdateValueImplement() ends
            // up calling UpdateValuesIfNeeded()

            if (_dirtyObservables.Count == 0)
                return false;

            bool changed = false;
            int currentIndex = 0;

            while (currentIndex < _dirtyObservables.Count)
            {
                var current = _dirtyObservables[currentIndex];

                if (current.Disposed || current.Observable == null
                    || !current.NeedsUpdate) // this may happen wen we are called recursively
                {
                    ++currentIndex;
                    continue;
                }

                // clear NeedsUpdate before we call UpdateValueImplementation to avoid infinite recursion
                current.NeedsUpdate = false; 

                if (current.Observable.UpdateValueImplementation())
                {
                    changed = true;
                    _changedObservables[current.Id] = current;
                }

                ++currentIndex;
            }

            _dirtyObservables.Clear();

            return changed;
        }

        private void PerformUpdates()
        {
            UpdateValuesIfNeeded();

            // Ok, now we are at a stable point, we can reset.
            // Any new changes (including those from the subscriptions
            // and tasks we are about to execute) will be handled in the
            // next update cycle.

            var tasks = _tasks;
            var changed = _changedObservables;
            _tasks = new();
            _changedObservables = new();

            NeedsUpdate = false;

            // notify changed objects' subscribers of changes...

            foreach (var observableState in changed)
                observableState.Value.InvokeSubscriptions();

            // Finally, execute any enqueued tasks...

            foreach (var task in tasks)
                task.Invoke();
        }

        private void SetNeedsUpdate()
        {
            // thread-safe-ish...

            if (Context == null)
                return; // we are waiting for a valid context...

            if (NeedsUpdate)
                return;

            NeedsUpdate = true;

            Context.Post((domain) => ((ObservableDomain)domain).PerformUpdates(), this);
        }

        public void Enqueue(Action task)
        {
            // thread safe
            _tasks.Enqueue(task);
            SetNeedsUpdate();
        }

        internal void ObservableNeedsUpdate(ObservableState state)
        {
            EnsureCurrentContext();

            if (state.Disposed)
                return;

            if (state.NeedsUpdate)
                return;

            state.NeedsUpdate = true;
            _dirtyObservables.Add(state);

            // notify dependencies they may need to be updated as well...
            // (recursive)

            if (state.Dependencies != null)
            {
                foreach (var weak in state.Dependencies)
                {
                    if (weak.TryGetTarget(out var dependency))
                        ObservableNeedsUpdate(dependency);
                }
            }

            SetNeedsUpdate();
        }

        private Dictionary<string, IObservableValue>? _captureGroup = null;

        internal void ObservableWillReadValue(ObservableState state)
        {
            EnsureCurrentContext();

            if (_captureGroup == null || state.Disposed || state.Observable == null)
                return;

            _captureGroup[state.Id] = state.Observable;
        }

        public IEnumerable<IObservableValue> CaptureReads(Action action)
        {
            EnsureCurrentContext();

            var prevCaptureGroup = _captureGroup;
            var captureGroup = new Dictionary<string, IObservableValue>();

            _captureGroup = captureGroup;
            action();
            _captureGroup = prevCaptureGroup;

            return captureGroup.Values;
        }
    }
}
