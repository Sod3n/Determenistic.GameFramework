using System;
using System.Runtime.InteropServices;

namespace Deterministic.GameFramework.CoreV2;

public class ActionScheduler
{
    // Struct to hold pending action metadata
    private struct PendingAction
    {
        public int NetworkId;
        public int TargetEntityId;
        public long ExecuteTick;
        public int DataOffset;
        public int DataLength;
    }

    private byte[] _actionDataBuffer = new byte[1024 * 16]; // 16KB initial buffer
    private int _actionDataHead = 0;
    
    private PendingAction[] _pendingActions = new PendingAction[1024];
    private int _pendingActionCount = 0;

    /// <summary>
    /// Delegate for the OnActionScheduled event.
    /// </summary>
    public delegate void ActionScheduledHandler(int networkId, ReadOnlySpan<byte> data, int targetEntityId, long executeTick);

    public event ActionScheduledHandler? OnActionScheduled;

    public long EarliestDirtyTick { get; private set; } = long.MaxValue;

    public void Schedule<TAction>(TAction action, int networkId, Entity target, long executeTick) where TAction : struct, IAction
    {
        int structSize = Marshal.SizeOf<TAction>();
        
        EnsureCapacity();
        EnsureDataCapacity(structSize);

        // Copy struct to buffer
#if NETSTANDARD2_1 || NETSTANDARD2_0
        MemoryMarshal.Write(new Span<byte>(_actionDataBuffer, _actionDataHead, structSize), ref action);
#else
        MemoryMarshal.Write(new Span<byte>(_actionDataBuffer, _actionDataHead, structSize), in action);
#endif

        // Record metadata
        AddPendingAction(networkId, target.Id, executeTick, _actionDataHead, structSize);

        // Notify listeners (Network Layer)
        if (OnActionScheduled != null)
        {
            var span = new ReadOnlySpan<byte>(_actionDataBuffer, _actionDataHead, structSize);
            OnActionScheduled.Invoke(networkId, span, target.Id, executeTick);
        }

        _actionDataHead += structSize;
        
        // Track for Rollback
        if (executeTick < EarliestDirtyTick) EarliestDirtyTick = executeTick;
    }

    public void ScheduleFromBytes(int networkId, ReadOnlySpan<byte> data, int targetEntityId, long executeTick)
    {
        // Deduplication: Check if identical action is already scheduled for this tick
        // This handles Client-Side Prediction (Local action matches Server action)
        if (IsDuplicate(networkId, targetEntityId, executeTick, data))
        {
            return;
        }

        int structSize = data.Length;
        
        EnsureCapacity();
        EnsureDataCapacity(structSize);

        // Copy bytes to buffer
        data.CopyTo(new Span<byte>(_actionDataBuffer, _actionDataHead, structSize));

        // Record metadata
        AddPendingAction(networkId, targetEntityId, executeTick, _actionDataHead, structSize);

        _actionDataHead += structSize;

        // Track for Rollback
        if (executeTick < EarliestDirtyTick) EarliestDirtyTick = executeTick;
    }

    private bool IsDuplicate(int networkId, int targetEntityId, long tick, ReadOnlySpan<byte> data)
    {
        for (int i = 0; i < _pendingActionCount; i++)
        {
            ref var pending = ref _pendingActions[i];
            
            // Fast checks
            if (pending.ExecuteTick != tick) continue;
            if (pending.NetworkId != networkId) continue;
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
        int countToExecute = 0;
        // Stackalloc for sorting indices
        Span<int> indicesToExecute = stackalloc int[_pendingActionCount]; 

        // 1. Identify valid actions STRICTLY for this tick
        for (int i = 0; i < _pendingActionCount; i++)
        {
            if (_pendingActions[i].ExecuteTick == tick)
            {
                indicesToExecute[countToExecute++] = i;
            }
        }
        
        // Optimization: If we are executing the EarliestDirtyTick, we can reset it because we are now "Clean" up to this point
        if (tick >= EarliestDirtyTick)
        {
             EarliestDirtyTick = long.MaxValue;
        }

        if (countToExecute == 0) return;

        // 2. Sort indices (Insertion Sort for determinism)
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

        // 3. Execute
        for (int i = 0; i < countToExecute; i++)
        {
            int idx = indicesToExecute[i];
            ref var pending = ref _pendingActions[idx];
            
            // Dispatch to service
            dispatcher.ExecuteByteAction(pending.NetworkId, _actionDataBuffer, pending.DataOffset, state, new Entity(pending.TargetEntityId));
        }
    }

    public void PruneHistory(long minTick)
    {
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

    private void AddPendingAction(int networkId, int targetId, long tick, int offset, int length)
    {
        _pendingActions[_pendingActionCount++] = new PendingAction
        {
            NetworkId = networkId,
            TargetEntityId = targetId,
            ExecuteTick = tick,
            DataOffset = offset,
            DataLength = length
        };
    }
}
