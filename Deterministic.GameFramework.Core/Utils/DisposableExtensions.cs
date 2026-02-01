using Deterministic.GameFramework.Core.Utils;

namespace Deterministic.GameFramework.Core.Extensions;

public static class DisposableExtensions
{
    public static IDisposable AddTo(this IDisposable disposable, CompositeDisposable disposables)
    {
        disposables.Add(disposable);
        return disposable;
    }
}