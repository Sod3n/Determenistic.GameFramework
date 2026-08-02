using System;
using Deterministic.GameFramework.ECS;

namespace Deterministic.GameFramework.Serialization;

public class StateHistory
{
    private struct Snapshot
    {
        public long Tick;
        public byte[] Data;
        public int Length;
    }

    private readonly Snapshot[] _buffer;
    private readonly int _capacity;

    private int _head;
    private int _tail;
    private int _count;

    private readonly object _lock = new object();

    public StateHistory(int capacity)
    {
        _capacity = capacity;
        _buffer = new Snapshot[capacity];
        _head = 0;
        _tail = 0;
        _count = 0;
    }

    public void Clear()
    {
        lock (_lock)
        {
            _head = 0;
            _tail = 0;
            _count = 0;
            for (int i = 0; i < _capacity; i++)
            {
                _buffer[i].Tick = 0;
                _buffer[i].Length = 0;
            }
        }
    }

    public void Store(long tick, EntityWorld state)
    {
        var (sharedBuf, length) = StateSerializer.SerializeInto(state);

        lock (_lock)
        {
            ref var slot = ref _buffer[_tail];

            if (slot.Data == null || slot.Data.Length < length)
                slot.Data = new byte[(int)(length * 1.25)];

            Buffer.BlockCopy(sharedBuf, 0, slot.Data, 0, length);
            slot.Length = length;
            slot.Tick = tick;

            if (_count < _capacity)
            {
                _count++;
            }
            else
            {
                _head = (_head + 1) % _capacity;
            }

            _tail = (_tail + 1) % _capacity;
        }
    }

    public bool Retrieve(long tick, EntityWorld state)
    {
        lock (_lock)
        {
            if (_count == 0) return false;

            long oldestTick = _buffer[_head].Tick;
            int latestIdx = (_tail - 1 + _capacity) % _capacity;
            long latestTick = _buffer[latestIdx].Tick;

            if (tick < oldestTick || tick > latestTick)
            {
                return false;
            }

            for (int i = 0; i < _count; i++)
            {
                int idx = (_head + i) % _capacity;
                if (_buffer[idx].Tick == tick)
                {
                    var data = _buffer[idx].Data;
                    var length = _buffer[idx].Length;
                    StateSerializer.Deserialize(state, data, length, syncComponentIds: false);
                    return true;
                }
            }
        }

        return false;
    }

    public void DiscardFuture(long tick)
    {
        lock (_lock)
        {
            if (_count == 0) return;

            while (_count > 0)
            {
                int latestIdx = (_tail - 1 + _capacity) % _capacity;
                if (_buffer[latestIdx].Tick > tick)
                {
                    _tail = latestIdx;
                    _count--;
                }
                else
                {
                    break;
                }
            }
        }
    }

    public long GetOldestTick()
    {
        lock (_lock)
        {
            if (_count == 0) return -1;
            return _buffer[_head].Tick;
        }
    }

    public long GetLatestTick()
    {
        lock (_lock)
        {
            if (_count == 0) return -1;
            int latestIdx = (_tail - 1 + _capacity) % _capacity;
            return _buffer[latestIdx].Tick;
        }
    }

    public bool TryGetSnapshotData(long tick, out byte[]? data)
    {
        lock (_lock)
        {
            data = null;
            if (_count == 0) return false;

            long oldestTick = _buffer[_head].Tick;
            int latestIdx = (_tail - 1 + _capacity) % _capacity;
            long latestTick = _buffer[latestIdx].Tick;

            if (tick < oldestTick || tick > latestTick)
            {
                return false;
            }

            for (int i = 0; i < _count; i++)
            {
                int idx = (_head + i) % _capacity;
                if (_buffer[idx].Tick == tick)
                {
                    int len = _buffer[idx].Length;
                    data = new byte[len];
                    Buffer.BlockCopy(_buffer[idx].Data, 0, data, 0, len);
                    return true;
                }
            }
        }

        return false;
    }
}
