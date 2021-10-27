using System;
using System.Collections.Generic;

namespace Concise.Observables
{
    public class DisposableCollection: IDisposable
    {
        private bool _disposed = false;
        private List<IDisposable> _disposables = new();

        public DisposableCollection()
        {
        }

        public DisposableCollection(Action action)
        {
            Capture(action);
        }

        public static DisposableCollection? Current = null;

        public void Capture(Action action)
        {
            var prev = Current;

            action();

            Current = prev;
        }

        public static void CaptureAdd(IDisposable disposable)
        {
            if (Current == null)
                throw new InvalidOperationException("CaptureAdd may only be called within a Capture block");

            Current.Add(disposable);
        }

        public void Add(IDisposable disposable)
        {
            if (_disposed)
                return;

            _disposables.Add(disposable);
        }

        public void Clear()
        {
            foreach (var disposable in _disposables)
                disposable.Dispose();

            _disposables.Clear();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                Clear();
            }

            _disposed = true;
        }


        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public override string ToString()
        {
            return $"DisposableCollection({String.Join(", ", _disposables)})";
        }
    }

    public static class DisposableCollectionExtensions
    {
        public static void AddTo(this IDisposable disposable, DisposableCollection disposableCollection) =>
            disposableCollection.Add(disposable);
    }
}
