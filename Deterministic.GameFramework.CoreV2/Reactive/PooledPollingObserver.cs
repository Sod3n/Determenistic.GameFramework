using System;
using System.Collections.Generic;

namespace Deterministic.GameFramework.CoreV2;

// Pool for observers to avoid allocations
internal static class ObserverPool<TObserver> where TObserver : new()
{
    private static readonly Stack<TObserver> _pool = new();

    public static TObserver Get()
    {
        if (_pool.Count > 0) return _pool.Pop();
        return new TObserver();
    }

    public static void Return(TObserver observer)
    {
        _pool.Push(observer);
    }
}

public class PollingObserver<TContext, TValue> : ObserverNode
{
    private TContext _context;
    private Func<TContext, TValue> _selector;
    private Action<TContext, TValue> _callback;
    private IEqualityComparer<TValue> _comparer;
    
    private TValue _previousValue;
    private bool _hasValue;

    // Parameterless constructor for pooling
    public PollingObserver() 
    {
        _context = default!;
        _selector = default!;
        _callback = default!;
        _comparer = default!;
        _previousValue = default!;
    }

    public void Initialize(TContext context, Func<TContext, TValue> selector, Action<TContext, TValue> callback, IEqualityComparer<TValue>? comparer)
    {
        _context = context;
        _selector = selector;
        _callback = callback;
        _comparer = comparer ?? EqualityComparer<TValue>.Default;
        
        _previousValue = default!;
        _hasValue = false;
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
        // Clear references to allow GC
        _context = default!;
        _selector = null!;
        _callback = null!;
        _comparer = null!;
        _hasValue = false;
        
        ObserverPool<PollingObserver<TContext, TValue>>.Return(this);
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
        _selector = null!;
        _callback = null!;
        _comparer = null!;
        _hasValue = false;
        
        ObserverPool<SimplePollingObserver<TValue>>.Return(this);
    }
}
