using System;
using System.Collections.Generic;

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
    private int _head;
    private int _count;

    public StateHistory(int capacity)
    {
        _capacity = capacity;
        _buffer = new Snapshot[capacity];
        _head = 0;
        _count = 0;
    }

    public void Store(long tick, GlobalState state)
    {
        byte[] data = StateSerializer.Serialize(state);
        
        // Overwrite or Add
        int index = (_head + _count) % _capacity;
        
        // If full, we overwrite the oldest (Head)
        if (_count == _capacity)
        {
            _head = (_head + 1) % _capacity;
            index = (_head + _count - 1) % _capacity; // Wait, if full, head moved, so we write to old head pos
            // Actually, simplest ring buffer logic:
            // Always write to (Head + Count) % Cap.
            // If Count == Cap, move Head.
        }

        if (_count < _capacity)
        {
            _buffer[index] = new Snapshot { Tick = tick, Data = data };
            _count++;
        }
        else
        {
            // Full, overwrite oldest
             // Current Head is oldest. We want to overwrite Head? 
             // No, we append to tail, but tail wraps to head.
             // So we overwrite head, and move head forward.
             int writePos = _head;
             _buffer[writePos] = new Snapshot { Tick = tick, Data = data };
             _head = (_head + 1) % _capacity;
        }
    }

    public bool Retrieve(long tick, GlobalState state)
    {
        // Find snapshot with Tick == tick
        for (int i = 0; i < _count; i++)
        {
            int idx = (_head + i) % _capacity;
            if (_buffer[idx].Tick == tick)
            {
                StateSerializer.Deserialize(state, _buffer[idx].Data);
                return true;
            }
        }
        return false;
    }

    public void DiscardFuture(long tick)
    {
        // We want to keep everything up to and including 'tick'.
        // Everything after 'tick' is discarded.
        
        for (int i = 0; i < _count; i++)
        {
            int idx = (_head + i) % _capacity;
            if (_buffer[idx].Tick == tick)
            {
                // We found the new 'latest' tick.
                // The new count is i + 1.
                _count = i + 1;
                return;
            }
            if (_buffer[idx].Tick > tick)
            {
                // We somehow skipped past it? (Shouldn't happen if sorted)
                // If we found something OLDER than tick, we keep going.
                // If we found something NEWER than tick, and we haven't found tick yet...
                // It means 'tick' isn't in history.
                // But typically we call DiscardFuture AFTER Retrieve(tick), so we know it exists.
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
        int tail = (_head + _count - 1) % _capacity;
        return _buffer[tail].Tick;
    }
}
