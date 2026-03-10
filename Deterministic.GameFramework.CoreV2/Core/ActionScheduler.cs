using System;
using System.Runtime.InteropServices;

namespace Deterministic.GameFramework.CoreV2;

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

    private byte[] _actionDataBuffer = new byte[1024 * 16]; // 16KB initial buffer
    private int _actionDataHead = 0;
    
    private PendingAction[] _pendingActions = new PendingAction[1024];
    private int _pendingActionCount = 0;
    
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
            // Deduplication
            if (IsDuplicate(id, targetEntityId, executeTick, data))
            {
                return ScheduleResult.Duplicate;
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

    public void ExecuteActions(long tick, GlobalState state, Dispatcher dispatcher)
    {
        ExecutableAction[] actionsToExecute;

        lock (_lock)
        {
            int count = 0;
            // 1. Identify valid actions STRICTLY for this tick
            for (int i = 0; i < _pendingActionCount; i++)
            {
                if (_pendingActions[i].ExecuteTick == tick)
                {
                    count++;
                }
            }
            
            // Optimization: If we are executing the EarliestDirtyTick, we can reset it because we are now "Clean" up to this point
            if (tick >= EarliestDirtyTick)
            {
                 EarliestDirtyTick = long.MaxValue;
            }

            if (count == 0) return;

            actionsToExecute = new ExecutableAction[count];
            int dst = 0;
            for (int i = 0; i < _pendingActionCount; i++)
            {
                ref var pending = ref _pendingActions[i];
                if (pending.ExecuteTick == tick)
                {
                    byte[] data = new byte[pending.DataLength];
                    Array.Copy(_actionDataBuffer, pending.DataOffset, data, 0, pending.DataLength);
                    
                    actionsToExecute[dst++] = new ExecutableAction
                    {
                        Id = pending.Id,
                        TargetEntityId = pending.TargetEntityId,
                        Data = data
                    };
                }
            }
        }

        // 2. Sort (Insertion Sort for determinism - or Array.Sort with stable key)
        Array.Sort(actionsToExecute, (a, b) => 
        {
            int netIdCompare = a.Id.CompareTo(b.Id);
            return netIdCompare != 0 ? netIdCompare : a.TargetEntityId.CompareTo(b.TargetEntityId);
        });

        // 3. Execute
        for (int i = 0; i < actionsToExecute.Length; i++)
        {
            var action = actionsToExecute[i];
            dispatcher.ExecuteByteAction(action.Id, action.Data, 0, state, new Entity(action.TargetEntityId));
        }
    }
    
    private struct ExecutableAction
    {
        public DenseComponentId Id;
        public int TargetEntityId;
        public byte[] Data;
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
            // Optimization: Only compact if > 4KB waste to avoid frequent memmoves
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
