using R3;

namespace Deterministic.GameFramework.Reactive;

public class Model : IDisposable
{
    protected readonly CompositeDisposable Disposables = new();

    public virtual void Dispose()
    {
        Disposables.Dispose();
    }
}