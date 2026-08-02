using System;

namespace Deterministic.GameFramework.DeltaSync;

/// <summary>
/// Fixed-size ring buffer of predicted deltas captured during the client's
/// <c>PostSimulate</c>. Slots are indexed by <c>tick % capacity</c> and reused —
/// the byte[] backing each slot grows only when a larger delta arrives (1.25x
/// grow pattern, mirroring <c>StateHistory</c>). Steady-state operation is
/// allocation-free.
///
/// Used by <see cref="DeltaSyncStrategyClient"/> to diff predicted-vs-server
/// state arithmetically: when the server's delta for tick <c>T</c> arrives,
/// the client retrieves the predicted delta it captured at <c>T</c> and
/// computes <c>correction = server_delta - predicted_delta</c>.
/// </summary>
public class PredictedDeltaRing
{
    private struct Slot
    {
        public long Tick;       // Tick this slot represents; -1 when empty
        public byte[]? Bytes;   // Backing buffer; reused across store calls
        public int Length;      // Actual data length (Bytes.Length may be larger)
    }

    private readonly Slot[] _slots;
    private readonly int _capacity;

    public PredictedDeltaRing(int capacity = 60)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
        _slots = new Slot[capacity];
        for (int i = 0; i < capacity; i++)
            _slots[i].Tick = -1;
    }

    public int Capacity => _capacity;

    /// <summary>
    /// Store a predicted delta for <paramref name="tick"/>. Overwrites any previous
    /// entry at the same slot (tick % capacity). Reuses the slot's byte[] if it's
    /// already large enough; otherwise grows with 25% headroom.
    /// </summary>
    public void Store(long tick, byte[] deltaBytes, int length)
    {
        if (deltaBytes == null) throw new ArgumentNullException(nameof(deltaBytes));
        if (length < 0 || length > deltaBytes.Length) throw new ArgumentOutOfRangeException(nameof(length));

        int idx = (int)((tick % _capacity + _capacity) % _capacity);
        ref var slot = ref _slots[idx];

        if (slot.Bytes == null || slot.Bytes.Length < length)
            slot.Bytes = new byte[(int)(length * 1.25) + 16];

        if (length > 0)
            Buffer.BlockCopy(deltaBytes, 0, slot.Bytes, 0, length);
        slot.Length = length;
        slot.Tick = tick;
    }

    /// <summary>
    /// Retrieve the predicted delta for <paramref name="tick"/>. Returns the slot's
    /// internal buffer (do not retain across subsequent Store calls for the same slot).
    /// </summary>
    public bool TryGet(long tick, out byte[] bytes, out int length)
    {
        int idx = (int)((tick % _capacity + _capacity) % _capacity);
        ref var slot = ref _slots[idx];
        if (slot.Tick == tick && slot.Bytes != null)
        {
            bytes = slot.Bytes;
            length = slot.Length;
            return true;
        }

        bytes = Array.Empty<byte>();
        length = 0;
        return false;
    }

    /// <summary>
    /// Mark all slots with <c>Tick &lt;= tick</c> as empty. Buffers are retained
    /// for reuse on subsequent Store calls.
    /// </summary>
    public void EvictUpTo(long tick)
    {
        for (int i = 0; i < _capacity; i++)
        {
            if (_slots[i].Tick >= 0 && _slots[i].Tick <= tick)
            {
                _slots[i].Tick = -1;
                _slots[i].Length = 0;
            }
        }
    }

    /// <summary>Empty every slot. Buffers are retained.</summary>
    public void Clear()
    {
        for (int i = 0; i < _capacity; i++)
        {
            _slots[i].Tick = -1;
            _slots[i].Length = 0;
        }
    }
}
