using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Deterministic.GameFramework.ECS;

namespace Deterministic.GameFramework.DAR;

/// <summary>
/// Manages the TEMPORAL scheduling of actions.
/// Responsible for:
/// 1. Buffering actions that are scheduled for future ticks.
/// 2. Deterministically ordering actions within a tick.
/// 3. Injecting actions into the EntityWorld at the start of their target tick.
/// 4. Managing history pruning and rollback triggers (detecting inputs for past ticks).
/// 
/// Does NOT execute logic. It only holds data until the correct time, then hands off to Dispatcher/ECS.
/// </summary>
public class ActionScheduler
{
    public enum ScheduleResult
    {
        Success,
        Duplicate,
        TooOld
    }

    // Struct to hold pending action metadata
    private struct PendingAction
    {
        public DenseComponentId Id;
        public int TargetEntityId;
        public long ExecuteTick;
        public int DataOffset;
        public int DataLength;
    }

    // Struct for execution sorting
    private struct ExecutableAction
    {
        public DenseComponentId Id;
        public int TargetEntityId;
        public int DataOffset;
        public int DataLength;
        public int Sequence;
    }
    
    // Comparer for ExecutableAction
    private class ExecutableActionComparer : IComparer<ExecutableAction>
    {
        public int Compare(ExecutableAction a, ExecutableAction b)
        {
            int netIdCompare = a.Id.CompareTo(b.Id);
            return netIdCompare != 0 ? netIdCompare : a.TargetEntityId.CompareTo(b.TargetEntityId);
        }
    }

    private byte[] _actionDataBuffer = new byte[1024 * 16]; // 16KB initial buffer
    private int _actionDataHead = 0;
    
    private PendingAction[] _pendingActions = new PendingAction[1024];
    private int _pendingActionCount = 0;
    
    // Reusable buffer for execution to avoid allocations
    private ExecutableAction[] _executionBuffer = new ExecutableAction[1024];
    
    // Singleton comparer to avoid allocations
    private static readonly ExecutableActionComparer _comparer = new ExecutableActionComparer();

    private readonly object _lock = new object();

    /// <summary>
    /// Delegate for the OnActionScheduled event.
    /// </summary>
    public delegate void ActionScheduledHandler(DenseComponentId id, ReadOnlySpan<byte> data, int targetEntityId, long executeTick);

    public event ActionScheduledHandler? OnActionScheduled;

    public long EarliestDirtyTick { get; private set; } = long.MaxValue;
    public long MinAllowedTick { get; private set; } = 0;

    public ScheduleResult Schedule<TAction>(TAction action, DenseComponentId id, Entity target, long executeTick) where TAction : struct, IAction
    {
        // Create ReadOnlySpan<byte> for struct
#if NETSTANDARD2_1 || NETSTANDARD2_0
        var actionSpan = MemoryMarshal.CreateReadOnlySpan(ref action, 1);
#else
        var actionSpan = MemoryMarshal.CreateReadOnlySpan(in action, 1);
#endif
        var byteSpan = MemoryMarshal.AsBytes(actionSpan);

        return ScheduleInternal(id, target.Id, executeTick, byteSpan);
    }

    public ScheduleResult ScheduleFromBytes(DenseComponentId id, ReadOnlySpan<byte> data, int targetEntityId, long executeTick)
    {
        if (executeTick < MinAllowedTick)
        {
            return ScheduleResult.TooOld;
        }
        return ScheduleInternal(id, targetEntityId, executeTick, data);
    }

    private ScheduleResult ScheduleInternal(DenseComponentId id, int targetEntityId, long executeTick, ReadOnlySpan<byte> data)
    {
        lock (_lock)
        {
            // 1. Deduplication (Exact Match)
            if (IsDuplicate(id, targetEntityId, executeTick, data))
            {
                return ScheduleResult.Duplicate;
            }

            // 2. Collision Resolution (Same Slot, Different Data) -> Bump Tick
            // We loop until we find a tick where this (Action, Target) slot is free.
            while (IsSlotOccupied(id, targetEntityId, executeTick))
            {
                executeTick++;
            }
            
            EnsureCapacity();
            EnsureDataCapacity(data.Length);

            // Copy bytes to buffer
            data.CopyTo(new Span<byte>(_actionDataBuffer, _actionDataHead, data.Length));

            // Record metadata
            AddPendingAction(id, targetEntityId, executeTick, _actionDataHead, data.Length);

            // Notify listeners (Network Layer)
            if (OnActionScheduled != null)
            {
                var span = new ReadOnlySpan<byte>(_actionDataBuffer, _actionDataHead, data.Length);
                OnActionScheduled.Invoke(id, span, targetEntityId, executeTick);
            }

            _actionDataHead += data.Length;
            
            // Track for Rollback
            if (executeTick < EarliestDirtyTick) EarliestDirtyTick = executeTick;
        }

        return ScheduleResult.Success;
    }

    private bool IsDuplicate(DenseComponentId id, int targetEntityId, long tick, ReadOnlySpan<byte> data)
    {
        for (int i = 0; i < _pendingActionCount; i++)
        {
            ref var pending = ref _pendingActions[i];
            
            // Fast checks
            if (pending.ExecuteTick != tick) continue;
            if (pending.Id != id) continue;
            if (pending.TargetEntityId != targetEntityId) continue;
            if (pending.DataLength != data.Length) continue;

            // Deep check
            var pendingSpan = new ReadOnlySpan<byte>(_actionDataBuffer, pending.DataOffset, pending.DataLength);
            if (pendingSpan.SequenceEqual(data))
            {
                return true;
            }
        }
        return false;
    }

    private bool IsSlotOccupied(DenseComponentId id, int targetEntityId, long tick)
    {
        for (int i = 0; i < _pendingActionCount; i++)
        {
            ref var pending = ref _pendingActions[i];
            
            if (pending.ExecuteTick == tick && 
                pending.Id == id && 
                pending.TargetEntityId == targetEntityId)
            {
                return true;
            }
        }
        return false;
    }

    public void ExecuteActions(long tick, EntityWorld state, Dispatcher dispatcher)
    {
        int count = 0;
        byte[] bufferToUse;

        lock (_lock)
        {
            // 1. Identify valid actions STRICTLY for this tick
            
            // Reset earliest dirty tick if we are processing it
            if (tick >= EarliestDirtyTick)
            {
                 EarliestDirtyTick = long.MaxValue;
            }

            // We iterate all pending actions. 
            for (int i = 0; i < _pendingActionCount; i++)
            {
                ref var pending = ref _pendingActions[i];
                if (pending.ExecuteTick == tick)
                {
                    if (count >= _executionBuffer.Length)
                    {
                        Array.Resize(ref _executionBuffer, _executionBuffer.Length * 2);
                    }
                    
                    _executionBuffer[count++] = new ExecutableAction
                    {
                        Id = pending.Id,
                        TargetEntityId = pending.TargetEntityId,
                        DataOffset = pending.DataOffset,
                        DataLength = pending.DataLength,
                        Sequence = i // Use index as stable sequence
                    };
                }
            }
            
            bufferToUse = _actionDataBuffer;
        }

        if (count == 0) return;

        // 2. Sort (Insertion Sort for stability and zero-allocation)
        for (int i = 1; i < count; i++)
        {
            var key = _executionBuffer[i];
            int j = i - 1;

            while (j >= 0)
            {
                ref var other = ref _executionBuffer[j];
                
                // Compare(key, other) < 0 ?
                int compare = key.Id.CompareTo(other.Id);
                if (compare == 0)
                {
                    compare = key.TargetEntityId.CompareTo(other.TargetEntityId);
                    if (compare == 0)
                    {
                        // Deterministic Tie-Breaker: Compare Content
                        // This ensures that arrival order (Sequence) doesn't dictate execution order for concurrent actions.
                        var spanKey = new ReadOnlySpan<byte>(bufferToUse, key.DataOffset, key.DataLength);
                        var spanOther = new ReadOnlySpan<byte>(bufferToUse, other.DataOffset, other.DataLength);
                        
                        compare = spanKey.SequenceCompareTo(spanOther);
                        
                        if (compare == 0)
                        {
                            compare = key.Sequence.CompareTo(other.Sequence);
                        }
                    }
                }

                if (compare < 0)
                {
                    _executionBuffer[j + 1] = other;
                    j--;
                }
                else
                {
                    break;
                }
            }
            _executionBuffer[j + 1] = key;
        }

        // 3. Execute
        for (int i = 0; i < count; i++)
        {
            ref var action = ref _executionBuffer[i];
            dispatcher.ExecuteByteAction(action.Id, bufferToUse, action.DataOffset, state, new Entity(action.TargetEntityId));
        }
    }

    public void PruneHistory(long minTick)
    {
        lock (_lock)
        {
            MinAllowedTick = minTick;
            int keepIdx = 0;
            int lowestValidOffset = _actionDataBuffer.Length; // Start high
            bool anyKept = false;
            long newMinTick = long.MaxValue;
            
            for (int i = 0; i < _pendingActionCount; i++)
            {
                ref var pending = ref _pendingActions[i];
                if (pending.ExecuteTick >= minTick) 
                {
                    _pendingActions[keepIdx] = pending;
                    if (pending.DataOffset < lowestValidOffset)
                    {
                        lowestValidOffset = pending.DataOffset;
                    }
                    
                    if (pending.ExecuteTick < newMinTick)
                    {
                        newMinTick = pending.ExecuteTick;
                    }

                    anyKept = true;
                    keepIdx++;
                }
            }

            _pendingActionCount = keepIdx;

            if (!anyKept)
            {
                _actionDataHead = 0;
                EarliestDirtyTick = long.MaxValue;
                return;
            }
            
            // If we pruned past the earliest dirty tick, update it to the earliest remaining action
            if (EarliestDirtyTick < minTick) 
            {
                EarliestDirtyTick = newMinTick;
            }

            // Compact Buffer if needed (only if we have significant waste)
            if (lowestValidOffset > 0 && lowestValidOffset > 4096) 
            {
                 int lengthToCopy = _actionDataHead - lowestValidOffset;
                 Array.Copy(_actionDataBuffer, lowestValidOffset, _actionDataBuffer, 0, lengthToCopy);
                 
                 // Update offsets
                 for(int i=0; i<_pendingActionCount; i++) {
                     _pendingActions[i].DataOffset -= lowestValidOffset;
                 }
                 _actionDataHead = lengthToCopy;
            }
        }
    }

    private void EnsureCapacity()
    {
        if (_pendingActionCount >= _pendingActions.Length)
        {
            Array.Resize(ref _pendingActions, _pendingActions.Length * 2);
        }
    }

    private void EnsureDataCapacity(int size)
    {
        if (_actionDataHead + size > _actionDataBuffer.Length)
        {
            Array.Resize(ref _actionDataBuffer, Math.Max(_actionDataBuffer.Length * 2, _actionDataHead + size));
        }
    }

    private void AddPendingAction(DenseComponentId id, int targetId, long tick, int offset, int length)
    {
        _pendingActions[_pendingActionCount++] = new PendingAction
        {
            Id = id,
            TargetEntityId = targetId,
            ExecuteTick = tick,
            DataOffset = offset,
            DataLength = length
        };
    }
}
