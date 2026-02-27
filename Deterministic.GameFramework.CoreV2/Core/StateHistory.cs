using System;

namespace Deterministic.GameFramework.CoreV2;

public class StateHistory
{
    private struct Snapshot
    {
        public long Tick;
        public byte[] Data;
    }

    private readonly Snapshot[] _buffer;
    private readonly int _capacity;
    
    // Points to the index of the OLDEST valid snapshot
    private int _head;
    // Points to the index where the NEXT snapshot will be written
    private int _tail;
    // Current number of snapshots stored
    private int _count;

    public StateHistory(int capacity)
    {
        _capacity = capacity;
        _buffer = new Snapshot[capacity];
        _head = 0;
        _tail = 0;
        _count = 0;
    }

    public void Store(long tick, GlobalState state)
    {
        byte[] data = StateSerializer.Serialize(state);
        
        _buffer[_tail] = new Snapshot { Tick = tick, Data = data };
        
        if (_count < _capacity)
        {
            _count++;
        }
        else
        {
            // Buffer is full, we overwrote the head (oldest)
            _head = (_head + 1) % _capacity;
        }
        
        _tail = (_tail + 1) % _capacity;
    }

    public bool Retrieve(long tick, GlobalState state)
    {
        if (_count == 0) return false;

        // Optimization: check bounds first
        long oldestTick = _buffer[_head].Tick;
        // Calculate latest tick safely. If _tail is 0, latest is at _capacity - 1
        int latestIdx = (_tail - 1 + _capacity) % _capacity;
        long latestTick = _buffer[latestIdx].Tick;

        if (tick < oldestTick || tick > latestTick)
        {
            return false;
        }

        // Linear search is fine for small buffers (60), but could use offset math if ticks are sequential
        for (int i = 0; i < _count; i++)
        {
            int idx = (_head + i) % _capacity;
            if (_buffer[idx].Tick == tick)
            {
                // DEBUG: Check mask before restore
                Console.WriteLine($"[StateHistory] Restoring Tick {tick}. Current Mask[0]: {state._entityMasks[0]._part0:X}");
                
                if (tick == 1)
                {
                     // Inspect data buffer at offset 10 (Start of Mask[0])
                     byte b = _buffer[idx].Data[10];
                     if (b != 0)
                     {
                         throw new Exception($"[StateHistory] Tick 1 Snapshot is Dirty! Byte 10: {b:X}");
                     }
                }

                StateSerializer.Deserialize(state, _buffer[idx].Data);
                
                // DEBUG: Check mask after restore
                Console.WriteLine($"[StateHistory] Restored. New Mask[0]: {state._entityMasks[0]._part0:X}");
                
                return true;
            }
        }
        
        return false;
    }

    public void DiscardFuture(long tick)
    {
        // Discard everything NEWER than 'tick'
        // Rewinding the 'tail'
        
        if (_count == 0) return;

        // Iterate backwards from newest
        while (_count > 0)
        {
            int latestIdx = (_tail - 1 + _capacity) % _capacity;
            if (_buffer[latestIdx].Tick > tick)
            {
                // Discard this one
                _tail = latestIdx; // Move tail back
                _count--;
            }
            else
            {
                // We found something <= tick. Stop discarding.
                break;
            }
        }
    }

    public long GetOldestTick()
    {
        if (_count == 0) return -1;
        return _buffer[_head].Tick;
    }

    public long GetLatestTick()
    {
        if (_count == 0) return -1;
        int latestIdx = (_tail - 1 + _capacity) % _capacity;
        return _buffer[latestIdx].Tick;
    }
}
