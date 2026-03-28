using R3;

namespace Deterministic.GameFramework.Reactive;

public class ViewModel : IDisposable
{
    public CompositeDisposable Disposables { get; private set; } = new();
    
    public virtual void Dispose()
    {
        Disposables.Dispose();
    }
}
