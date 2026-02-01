using Newtonsoft.Json;

namespace Deterministic.GameFramework.Core.Utils;

[JsonObject(MemberSerialization.OptIn)]
public class CompositeDisposable : IDisposable
{
    private List<IDisposable> _disposables = new();
    private bool _disposed;
    
    public List<IDisposable> Disposables => _disposables;

    public void Add(IDisposable disposable)
    {
        if (_disposed)
        {
            disposable?.Dispose();
            return;
        }
        _disposables.Add(disposable);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var disposable in _disposables)
        {
            disposable?.Dispose();
        }
        _disposables.Clear();
    }
}