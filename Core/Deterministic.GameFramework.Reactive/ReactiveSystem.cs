using System.Collections.Generic;
using System.Diagnostics;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Common;
using R3;
using System.Runtime.CompilerServices;
using Deterministic.GameFramework.Utils.Logging;
using System;

[assembly: InternalsVisibleTo("Deterministic.GameFramework.Reactive.Tests")]

namespace Deterministic.GameFramework.Reactive;

public class ReactiveSystem : IDisposable
{
    public static ReactiveSystem Instance { get; } = new();

    // Array-backed storage for cache-friendly iteration.
    // Swap-remove on Unregister keeps add/remove at O(1).
    private ObserverNode[] _observers = new ObserverNode[256];
    private int _count;

    private GameLoop? _boundLoop;
    private EntityWorld? _boundState;
    private readonly Action _tickDelegate;
    private Dictionary<Type, ObserverNode>? _batchObservers;

    public EntityWorld? BoundState => _boundState;
    public bool IsResimulating => _boundLoop?.IsResimulating ?? false;

    /// <summary>
    /// When true, observers skip CheckAndNotify — no add/remove callbacks fire.
    /// Set by GameClient while the state is unverified (between mismatch detection
    /// and state correction). Prevents visual flickering from temporary entity
    /// ID mismatches during the prediction/correction window.
    /// When unpaused, a Reset() is automatically triggered to reconcile state.
    /// </summary>
    public bool IsPaused
    {
        get => _isPaused;
        set
        {
            if (_isPaused && !value)
            {
                // Unpausing — reset all observers to reconcile with current state
                _isPaused = false;
                Reset();
                return;
            }
            _isPaused = value;
        }
    }
    private bool _isPaused;

    // Profiling
    private readonly Stopwatch _tickStopwatch = new();
    private int _observerCount;

    /// <summary>Time spent in last Tick() processing all observers.</summary>
    public double LastTickSeconds { get; private set; }

    /// <summary>Number of active observers processed in last tick.</summary>
    public int LastObserverCount => _observerCount;

    /// <summary>Total observers registered since last reset of this counter.</summary>
    public int TotalRegistered { get; private set; }
    /// <summary>Total observers unregistered since last reset of this counter.</summary>
    public int TotalUnregistered { get; private set; }

    public ReactiveSystem()
    {
        _tickDelegate = Tick;
    }

    public void Bind(EntityWorld state, GameLoop loop)
    {
        if (_boundLoop != null)
        {
            if (_boundLoop == loop) return;
            Unbind();
        }

        _boundLoop = loop;
        _boundState = state;
        loop.OnTick += _tickDelegate;
    }

    public void Unbind()
    {
        if (_boundLoop != null)
        {
            _boundLoop.OnTick -= _tickDelegate;
            _boundLoop = null;
        }
        _boundState = null;
    }

    public void Dispose()
    {
        Unbind();

        for (int i = _count - 1; i >= 0; i--)
            _observers[i].Dispose();
        _count = 0;
        _batchObservers?.Clear();
    }

    internal BatchComponentObserver<T> GetOrCreateBatchObserver<T>(EntityWorld context) where T : struct, IComponent
    {
        _batchObservers ??= new Dictionary<Type, ObserverNode>();
        if (_batchObservers.TryGetValue(typeof(T), out var existing))
            return (BatchComponentObserver<T>)existing;

        var batch = new BatchComponentObserver<T>();
        batch.Initialize(context);
        _batchObservers[typeof(T)] = batch;
        Register(batch, evaluateImmediately: false);
        return batch;
    }

    public void Register(ObserverNode node, bool evaluateImmediately = true)
    {
        if (node.Owner != null) throw new InvalidOperationException("Observer already registered to a system");

        node.Owner = this;

        // Append to array
        if (_count == _observers.Length)
            Array.Resize(ref _observers, _observers.Length * 2);
        node.Index = _count;
        _observers[_count++] = node;

        TotalRegistered++;
        if (TotalRegistered == 15000 || TotalRegistered == 20000 || TotalRegistered == 30000)
            ILogger.Log($"[ReactiveSystem] Register #{TotalRegistered}: {node.GetType().Name} from:\n{Environment.StackTrace}\n\n");
        // Perform an eager evaluation so observers have an initial value
        // as soon as they are registered, instead of waiting for the next Tick.
        if (evaluateImmediately && !IsResimulating)
        {
            try
            {
                node.CheckAndNotify();
            }
            catch (Exception ex)
            {
                ILogger.LogError($"[ReactiveSystem] Error in observer (initial): {ex}");
            }
        }
    }

    internal void Unregister(ObserverNode node)
    {
        if (node.Owner != this) return;
        TotalUnregistered++;

        // Swap-remove: move the last element into the removed slot
        int idx = node.Index;
        int last = _count - 1;
        if (idx < last)
        {
            var moved = _observers[last];
            _observers[idx] = moved;
            moved.Index = idx;
        }
        _observers[last] = null!;
        _count--;

        node.Owner = null;
        node.Index = -1;
    }

    public void Tick()
    {
        if (IsResimulating || _isPaused) return;

        _tickStopwatch.Restart();

        // Forward iteration. If a node removes itself (swap-remove), the
        // swapped-in element lands at the current index — re-visit it by
        // not advancing i. This preserves registration order.
        int i = 0;
        while (i < _count)
        {
            int before = _count;
            try
            {
                _observers[i].CheckAndNotify();
            }
            catch (Exception ex)
            {
                ILogger.LogError($"[ReactiveSystem] Error in observer: {ex}");
            }
            if (_count >= before) i++;
            // else: swap-remove happened, re-visit index i
        }

        _tickStopwatch.Stop();
        LastTickSeconds = _tickStopwatch.Elapsed.TotalSeconds;
        _observerCount = _count;
    }

    public void Reset()
    {
        for (int i = 0; i < _count; i++)
        {
            try
            {
                _observers[i].Reset();
            }
            catch (Exception ex)
            {
                ILogger.LogError($"[ReactiveSystem] Error resetting observer: {ex}");
            }
        }
    }
}
