using System;
using System.Collections.Generic;
using Deterministic.GameFramework.CoreV2;
using R3;

namespace Deterministic.GameFramework.Reactive;

public class ReactiveSystem : IDisposable
{
    public static ReactiveSystem Instance { get; } = new();

    private ObserverNode? _head;
    private ObserverNode? _tail;
    
    private GameLoop? _boundLoop;
    private GlobalState? _boundState;
    private bool _wasResimulating;

    public GlobalState? BoundState => _boundState;
    public bool IsResimulating => _boundLoop?.IsResimulating ?? false;

    public void Bind(GlobalState state)
    {
        if (_boundLoop != null)
        {
            if (_boundLoop == state.GameLoop) return; // Already bound to this loop
            Unbind(); // Unbind from previous loop
        }

        _boundLoop = state.GameLoop;
        _boundState = state;
        state.GameLoop.OnTick += Tick;
    }

    public void Unbind()
    {
        if (_boundLoop != null)
        {
            _boundLoop.OnTick -= Tick;
            _boundLoop = null;
        }
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

    public void Register(ObserverNode node)
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
            ResetAll();
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

    private void ResetAll()
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
