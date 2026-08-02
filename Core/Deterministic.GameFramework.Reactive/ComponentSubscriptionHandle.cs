using Deterministic.GameFramework.ECS;

namespace Deterministic.GameFramework.Reactive;

internal class ComponentSubscriptionHandle<T> : IDisposable where T : struct, IComponent
{
    internal BatchComponentObserver<T>? Batch;
    internal int EntryIndex;
    private bool _isDisposed;

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        Batch?.RemoveEntry(this);
        Batch = null;
    }

    internal void Reset()
    {
        _isDisposed = false;
        Batch = null;
        EntryIndex = -1;
    }
}
