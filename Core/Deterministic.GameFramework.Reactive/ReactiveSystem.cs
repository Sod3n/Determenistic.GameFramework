using System.Collections.Generic;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Common;
using R3;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Deterministic.GameFramework.Reactive.Tests")]

namespace Deterministic.GameFramework.Reactive;

public class ReactiveSystem : IDisposable
{
    public static ReactiveSystem Instance { get; } = new();

    private ObserverNode? _head;
    private ObserverNode? _tail;
    
    private GameLoop? _boundLoop;
    private EntityWorld? _boundState;
    private bool _wasResimulating;
    private readonly Action _tickDelegate;

    public EntityWorld? BoundState => _boundState;
    public bool IsResimulating => _boundLoop?.IsResimulating ?? false;

    public ReactiveSystem()
    {
        _tickDelegate = Tick;
    }

    public void Bind(EntityWorld state, GameLoop loop)
    {
        if (_boundLoop != null)
        {
            if (_boundLoop == loop) return; // Already bound to this loop
            Unbind(); // Unbind from previous loop
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
        
        // Dispose all nodes
        var node = _head;
        while (node != null)
        {
            var next = node.Next;
            node.Dispose(); // Will call Unregister, but we are clearing anyway
            node = next;
        }
        _head = null;
        _tail = null;
    }

    public void Register(ObserverNode node, bool evaluateImmediately = true)
    {
        if (node.Owner != null) throw new InvalidOperationException("Observer already registered to a system");
        
        node.Owner = this;
        
        // Add to tail
        if (_tail == null)
        {
            _head = _tail = node;
        }
        else
        {
            _tail.Next = node;
            node.Prev = _tail;
            _tail = node;
        }

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
                Console.WriteLine($"[ReactiveSystem] Error in observer (initial): {ex}");
            }
        }
    }

    internal void Unregister(ObserverNode node)
    {
        if (node.Owner != this) return;
        
        // Unlink
        if (node.Prev != null) node.Prev.Next = node.Next;
        else _head = node.Next; // Was head

        if (node.Next != null) node.Next.Prev = node.Prev;
        else _tail = node.Prev; // Was tail

        node.Next = null;
        node.Prev = null;
        node.Owner = null;
    }

    public void Tick()
    {
        bool isResimulating = IsResimulating;
        
        // Detect transition from Resimulating -> Normal (End of Rollback)
        if (!isResimulating && _wasResimulating)
        {
            _wasResimulating = false;
            Reset();
            return;
        }

        _wasResimulating = isResimulating;
        if (isResimulating) return; // Don't update observers during resimulation

        var node = _head;
        while (node != null)
        {
            // Cache next in case node removes itself during callback
            var next = node.Next;
            
            try
            {
                node.CheckAndNotify();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ReactiveSystem] Error in observer: {ex}");
            }
            
            node = next;
        }
    }

    public void Reset()
    {
        var node = _head;
        while (node != null)
        {
            var next = node.Next;
            try
            {
                node.Reset();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ReactiveSystem] Error resetting observer: {ex}");
            }
            node = next;
        }
    }
}
