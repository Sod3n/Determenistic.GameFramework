using Deterministic.GameFramework.Core.Utils;

namespace Deterministic.GameFramework.Core;

public class ObservableAttribute<TValue>(TValue initialValue = default)
{
    private TValue _value = initialValue;
    private event Action<TValue> OnChangeAction;
    
    public TValue Value
    {
        get => _value;
        set
        {
            if (EqualityComparer<TValue>.Default.Equals(_value, value)) return;
            _value = value;
            OnChangeAction?.Invoke(value);
        }
    }
    

    public IDisposable Observe(LeafDomain observer, Action<TValue> callback, bool fireImmediately = true)
    {
        if (fireImmediately) callback(_value);
        OnChangeAction += callback;
        var disposable = new ActionDisposable(() => OnChangeAction -= callback);
        observer.Disposables.Add(disposable);
        return disposable;
    }
}

public class ActionDisposable(Action action) : IDisposable
{
    public void Dispose()
    {
        action?.Invoke();
    }
}