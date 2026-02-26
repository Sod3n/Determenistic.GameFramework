using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Deterministic.GameFramework.CoreV2;

public class Dispatcher
{
    private readonly Dictionary<int, Action<object, GlobalState, Entity>> _actionRunners = new();
    private readonly Dictionary<int, Action<byte[], int, GlobalState, Entity>> _byteActionRunners = new();
    
    // Map Action Struct Type -> Service Network ID
    private readonly Dictionary<Type, int> _actionTypeToNetworkId = new();

    // Zero-allocation queueing
    private byte[] _actionDataBuffer = new byte[1024 * 16]; // 16KB initial buffer
    private int _actionDataHead = 0;
    
    private PendingAction[] _pendingActions = new PendingAction[1024];
    private int _pendingActionCount = 0;

    private readonly Func<Type, int>? _serviceIdLookup;

    public Dispatcher(Func<Type, int>? serviceIdLookup = null)
    {
        _serviceIdLookup = serviceIdLookup;
    }

    public void RegisterAction<TAction, TTarget>(
        ActionService<TAction, TTarget> actionService, 
        IEnumerable<ReactionService<TAction, TTarget>> reactions)
        where TAction : struct, IAction
        where TTarget : struct, IComponent
    {
        // Resolve NetworkId from the Service class attribute
        var serviceType = actionService.GetType();
        int networkId;

        if (_serviceIdLookup != null)
        {
            try 
            {
                networkId = _serviceIdLookup(serviceType);
            }
            catch (KeyNotFoundException)
            {
                // Fallback or rethrow? 
                // If a lookup is provided but fails, it means the generator didn't pick it up or it's missing the attribute.
                // Let's try reflection as fallback or just fail.
                // Given the goal is optimization, let's assume if lookup is provided it SHOULD be there.
                 throw new Exception($"ActionService {serviceType.Name} not found in provided NetworkId registry.");
            }
        }
        else
        {
            var networkIdAttr = serviceType.GetCustomAttribute<NetworkIdAttribute>();
            if (networkIdAttr == null)
            {
                throw new Exception($"ActionService {serviceType.Name} is missing [NetworkId] attribute.");
            }
            networkId = networkIdAttr.Id;
        }

        // Map the Action Type to this Network ID
        if (_actionTypeToNetworkId.ContainsKey(typeof(TAction)))
        {
            throw new Exception($"Action Type {typeof(TAction).Name} is already registered to ID {_actionTypeToNetworkId[typeof(TAction)]}. Cannot register multiple services for the same Action struct in this implementation.");
        }
        _actionTypeToNetworkId[typeof(TAction)] = networkId;

        var sortedReactions = reactions.OrderByDescending(r => r.Priority).ToList();
        var preReactions = sortedReactions.Where(r => !r.AfterActionExecuted).ToList();
        var postReactions = sortedReactions.Where(r => r.AfterActionExecuted).ToList();

        Action<object, GlobalState, Entity> runner = (actionObj, state, entity) =>
        {
            var action = (TAction)actionObj;
            var ctx = new Context(state, entity);
            ref var target = ref state.GetState<TTarget>(entity);

            if (RunPreReactions(action, ref target, ctx, preReactions))
                return;

            actionService.InternalExecute(action, ref target, ctx);

            RunPostReactions(action, ref target, ctx, postReactions);
        };
        
        Action<byte[], int, GlobalState, Entity> byteRunner = (buffer, offset, state, entity) =>
        {
            // Deserialize struct from raw bytes
            var span = new ReadOnlySpan<byte>(buffer, offset, Marshal.SizeOf<TAction>());
            var action = MemoryMarshal.Read<TAction>(span);
            
            var ctx = new Context(state, entity);
            ref var target = ref state.GetState<TTarget>(entity);

            if (RunPreReactions(action, ref target, ctx, preReactions))
                return;

            actionService.InternalExecute(action, ref target, ctx);

            RunPostReactions(action, ref target, ctx, postReactions);
        };

        _actionRunners[networkId] = runner;
        _byteActionRunners[networkId] = byteRunner;
    }

    public void Execute<TAction>(TAction action, GlobalState state, Entity entity) where TAction : struct, IAction
    {
        if (!_actionTypeToNetworkId.TryGetValue(typeof(TAction), out int networkId))
        {
             throw new Exception($"No registered service found for action {typeof(TAction).Name}");
        }

        if (_actionRunners.TryGetValue(networkId, out var runner))
        {
            runner(action, state, entity);
        }
        else
        {
            throw new Exception($"No runner registered for ID {networkId}");
        }
    }

    public void Schedule<TAction>(TAction action, Entity target, long executeTick) where TAction : struct, IAction
    {
        if (!_actionTypeToNetworkId.TryGetValue(typeof(TAction), out int networkId))
        {
             throw new Exception($"No registered service found for action {typeof(TAction).Name}. Cannot schedule.");
        }

        if (_pendingActionCount >= _pendingActions.Length)
        {
            Array.Resize(ref _pendingActions, _pendingActions.Length * 2);
        }

        int structSize = Marshal.SizeOf<TAction>();
        if (_actionDataHead + structSize > _actionDataBuffer.Length)
        {
            Array.Resize(ref _actionDataBuffer, _actionDataBuffer.Length * 2);
        }

        // Copy struct to buffer
        MemoryMarshal.Write(new Span<byte>(_actionDataBuffer, _actionDataHead, structSize), in action);

        // Record metadata
        _pendingActions[_pendingActionCount++] = new PendingAction
        {
            NetworkId = networkId,
            TargetEntityId = target.Id,
            ExecuteTick = executeTick,
            DataOffset = _actionDataHead,
            DataLength = structSize
        };

        _actionDataHead += structSize;
    }

    public void DrainScheduledActions(long currentTick, GlobalState state)
    {
        // 1. Identify valid actions for this tick
        // We do a partial sort or filter. To ensure determinism, we must execute actions in a stable order.
        // Sorting criteria: 1. NetworkId (Service ID), 2. TargetEntityId.
        // Since we want to avoid allocating a new list, we can sort the _pendingActions array or a slice of it?
        // But _pendingActions contains future actions too.
        
        // Simple approach for PoC: 
        // Iterate to find execute-able indices, put them in a temporary span/list, sort that, then execute.
        // To avoid allocation, we could swap them to the front or use a pooled index array.
        // For now, let's use a simple stack-alloc or pooled list if possible, or just a list for clarity then optimize.
        
        // Optimization: Use a struct-based comparer and `Array.Sort` over the valid range? 
        // But the valid actions might be sparse (interleaved with future actions).
        
        // Let's settle on: Execute all <= currentTick.
        // But the execution order must be deterministic.
        // If we process the array linearly, the order is "Schedule Order".
        // Networked games often require "Schedule Order" OR "Deterministic Sort".
        // If packets arrive out of order, "Schedule Order" might vary if we process packets immediately.
        // But usually packets are buffered for a tick.
        // Let's assume strict sorting is requested.
        
        int countToExecute = 0;
        // We'll use a small scratch buffer for indices to sort.
        Span<int> indicesToExecute = stackalloc int[_pendingActionCount]; 

        for (int i = 0; i < _pendingActionCount; i++)
        {
            if (_pendingActions[i].ExecuteTick <= currentTick && _pendingActions[i].ExecuteTick != -1)
            {
                indicesToExecute[countToExecute++] = i;
            }
        }

        if (countToExecute == 0) return;

        // Sort the indices based on the data in _pendingActions
        // Bubblesort or similar for small stackalloc? Or just copy to array and sort.
        // Since Span doesn't support custom lambda sort easily without allocations, let's do a simple insertion sort on the indices.
        // It's usually small count per tick.
        
        for (int i = 1; i < countToExecute; i++)
        {
            int keyIndex = indicesToExecute[i];
            var keyAction = _pendingActions[keyIndex];
            int j = i - 1;

            while (j >= 0)
            {
                int otherIndex = indicesToExecute[j];
                var otherAction = _pendingActions[otherIndex];
                
                // Sort Key: NetworkId ASC, then TargetEntityId ASC
                bool swap = false;
                if (otherAction.NetworkId > keyAction.NetworkId)
                {
                    swap = true;
                }
                else if (otherAction.NetworkId == keyAction.NetworkId && otherAction.TargetEntityId > keyAction.TargetEntityId)
                {
                    swap = true;
                }

                if (swap)
                {
                    indicesToExecute[j + 1] = indicesToExecute[j];
                    j--;
                }
                else
                {
                    break;
                }
            }
            indicesToExecute[j + 1] = keyIndex;
        }

        // Execute in sorted order
        for (int i = 0; i < countToExecute; i++)
        {
            int idx = indicesToExecute[i];
            ref var pending = ref _pendingActions[idx];
            
            if (_byteActionRunners.TryGetValue(pending.NetworkId, out var byteRunner))
            {
                byteRunner(_actionDataBuffer, pending.DataOffset, state, new Entity(pending.TargetEntityId));
            }
            
            // Mark as executed
            pending.ExecuteTick = -1; 
        }

        CompactPendingActions();
    }

    private void CompactPendingActions()
    {
        int keepIdx = 0;
        int lowestValidOffset = _actionDataHead;

        // Compact the pending actions array, keeping only those that haven't executed yet
        for (int i = 0; i < _pendingActionCount; i++)
        {
            ref var pending = ref _pendingActions[i];
            if (pending.ExecuteTick != -1) // Not executed
            {
                _pendingActions[keepIdx] = pending;
                if (pending.DataOffset < lowestValidOffset)
                {
                    lowestValidOffset = pending.DataOffset;
                }
                keepIdx++;
            }
        }

        _pendingActionCount = keepIdx;

        // If all actions executed, we can reset the buffer head entirely
        if (_pendingActionCount == 0)
        {
            _actionDataHead = 0;
        }
        else if (lowestValidOffset > 0 && lowestValidOffset > _actionDataBuffer.Length / 2)
        {
            // Optional: If we have a lot of dead space at the front of the buffer, we could memmove the live bytes to the front 
            // to prevent the buffer from growing indefinitely. 
            // For now, doing simple reset when empty is usually enough for deterministic lockstep if actions are processed frequently.
        }
    }

    private bool RunPreReactions<TAction, TTarget>(
        TAction action, 
        ref TTarget target, 
        Context ctx, 
        List<ReactionService<TAction, TTarget>> preReactions)
        where TAction : struct, IAction
        where TTarget : struct, IComponent
    {
        foreach (var reaction in preReactions)
        {
            var isAborted = reaction.InternalReact(action, ref target, ctx);
            if (isAborted.Value) return true; // Aborted
        }
        return false;
    }

    private void RunPostReactions<TAction, TTarget>(
        TAction action, 
        ref TTarget target, 
        Context ctx, 
        List<ReactionService<TAction, TTarget>> postReactions)
        where TAction : struct, IAction
        where TTarget : struct, IComponent
    {
        foreach (var reaction in postReactions)
        {
            reaction.InternalReact(action, ref target, ctx);
        }
    }
}
