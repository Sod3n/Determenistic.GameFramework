namespace Deterministic.GameFramework.Reactive;

public abstract class ObserverNode : IDisposable
{
    internal ObserverNode? Next;
    internal ObserverNode? Prev;
    internal ReactiveSystem? Owner;

    public abstract void CheckAndNotify();
    
    /// <summary>
    /// Called when the system detects a discontinuous state change (e.g. after rollback/resimulation).
    /// Observers should re-validate their state against the current truth.
    /// Default implementation just calls CheckAndNotify.
    /// </summary>
    public virtual void Reset() 
    {
        CheckAndNotify();
    }
    
    public void Dispose()
    {
        Owner?.Unregister(this);
        OnDispose();
    }

    protected virtual void OnDispose() { }
}
