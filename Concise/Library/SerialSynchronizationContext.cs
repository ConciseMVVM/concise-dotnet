using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Concise.Library
{
    public sealed class SerialSynchronizationContext : SynchronizationContext, IDisposable
    {
        readonly BlockingCollection<Action> _queue = new();
        readonly Thread _thread = new(ThreadWorker);

        public int WorkerThreadId => _thread.ManagedThreadId;

        public SerialSynchronizationContext() =>
            _thread.Start(this);

        public void Dispose() =>
            _queue.CompleteAdding();

        public override void Post(SendOrPostCallback d, object state) =>
            _queue.Add(() => d(state));

        public override void Send(SendOrPostCallback d, object state)
        {
            using (var manualResetEvent = new ManualResetEvent(false))
            {
                _queue.Add(() =>
                {
                    d(state);
                    manualResetEvent.Set();
                });

                manualResetEvent.WaitOne();
            }
        }

        static void ThreadWorker(object obj)
        {
            var ctx = (SerialSynchronizationContext)obj;

            SetSynchronizationContext(ctx);

            try
            {
                foreach (var action in ctx._queue.GetConsumingEnumerable())
                    action();
            }
            catch (ObjectDisposedException)
            {
                // thread exiting via CompleteAdding()
            }
        }
    }
}
