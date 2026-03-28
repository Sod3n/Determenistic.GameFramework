using System.Collections.Generic;
using Deterministic.GameFramework.ECS;

namespace Deterministic.GameFramework.Reactive;

// Pool for observers to avoid allocations
internal static class ObserverPool<TObserver> where TObserver : new()
{
    private static readonly Stack<TObserver> _pool = new();
    private static readonly object _lock = new();

    public static TObserver Get()
    {
        lock (_lock)
        {
            if (_pool.Count > 0) return _pool.Pop();
        }
        return new TObserver();
    }

    public static void Return(TObserver observer)
    {
        lock (_lock)
        {
            _pool.Push(observer);
        }
    }
}

public class PollingObserver<TValue> : ObserverNode
{
    private EntityWorld _context;
    private Func<EntityWorld, TValue> _selector;
    private Action<EntityWorld, TValue> _callback;
    private IEqualityComparer<TValue> _comparer;
    
    private TValue _previousValue;
    private bool _hasValue;
    private bool _isDisposed;

    // Parameterless constructor for pooling
    public PollingObserver() 
    {
        _context = default!;
        _selector = default!;
        _callback = default!;
        _comparer = default!;
        _previousValue = default!;
    }

    public void Initialize(EntityWorld context, Func<EntityWorld, TValue> selector, Action<EntityWorld, TValue> callback, IEqualityComparer<TValue>? comparer)
    {
        _context = context;
        _selector = selector;
        _callback = callback;
        _comparer = comparer ?? EqualityComparer<TValue>.Default;
        
        _previousValue = default!;
        _hasValue = false;
        _isDisposed = false;
    }

    public override void CheckAndNotify()
    {
        var currentValue = _selector(_context);
        
        if (!_hasValue || !_comparer.Equals(_previousValue, currentValue))
        {
            _previousValue = currentValue;
            _hasValue = true;
            _callback(_context, currentValue);
        }
    }

    protected override void OnDispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        // Clear references to allow GC
        _context = default!;
        _selector = null!;
        _callback = null!;
        _comparer = null!;
        _hasValue = false;
        
        ObserverPool<PollingObserver<TValue>>.Return(this);
    }
}

// Simple wrapper for non-context closures (legacy/simple support)
public class SimplePollingObserver<TValue> : ObserverNode
{
    private Func<TValue> _selector;
    private Action<TValue> _callback;
    private IEqualityComparer<TValue> _comparer;
    
    private TValue _previousValue;
    private bool _hasValue;
    private bool _isDisposed;

    public SimplePollingObserver() 
    {
        _selector = default!;
        _callback = default!;
        _comparer = default!;
        _previousValue = default!;
    }

    public void Initialize(Func<TValue> selector, Action<TValue> callback, IEqualityComparer<TValue>? comparer)
    {
        _selector = selector;
        _callback = callback;
        _comparer = comparer ?? EqualityComparer<TValue>.Default;
        
        _previousValue = default!;
        _hasValue = false;
        _isDisposed = false;
    }

    public override void CheckAndNotify()
    {
        var currentValue = _selector();
        
        if (!_hasValue || !_comparer.Equals(_previousValue, currentValue))
        {
            _previousValue = currentValue;
            _hasValue = true;
            _callback(currentValue);
        }
    }

    protected override void OnDispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _selector = null!;
        _callback = null!;
        _comparer = null!;
        _hasValue = false;
        
        ObserverPool<SimplePollingObserver<TValue>>.Return(this);
    }
}
