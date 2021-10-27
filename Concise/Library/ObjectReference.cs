using System;
namespace Concise.Library
{
    public class ObjectReference<T> where T : class
    {
        public bool IsWeak => _weakReference != null;
        T? _strongReference;
        WeakReference? _weakReference;

        public bool IsAlive => _weakReference?.IsAlive ?? true;
        public T? Target => (T?)_weakReference?.Target ?? _strongReference;

        public ObjectReference(T target, bool isWeak = false)
        {
            _strongReference = (isWeak) ? null : target;
            _weakReference = (isWeak) ? new WeakReference(target) : null;
        }
    }
}
