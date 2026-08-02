using System;
using System.Runtime.CompilerServices;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Utils.Logging;

namespace Deterministic.GameFramework.Reactive;

internal struct WatchEntry<T> where T : struct, IComponent
{
    public int EntityId;
    public Action<T>? Sync;
    public ComponentSubscriptionHandle<T>? Handle;
}

public class BatchComponentObserver<T> : ObserverNode where T : struct, IComponent
{
    private EntityWorld _context = null!;
    private AlignedComponentStore<T>? _store;
    private int _elemSize;
    private int _stride;

    private WatchEntry<T>[] _entries = new WatchEntry<T>[16];
    private int _count;

    // Full snapshot of the store buffer from previous tick
    private byte[] _snapshot = Array.Empty<byte>();
    private int _snapshotLength;
    private bool _hasSnapshot;

    private List<int>? _pendingRemoves;
    private bool _isTicking;

    internal void Initialize(EntityWorld context)
    {
        _context = context;
        _store = context.GetComponentStore<T>();
        _elemSize = _store?.ElementSize ?? 0;
        _stride = _store?.Stride ?? 0;
    }

    internal int AddEntry(int entityId, Action<T> sync, ComponentSubscriptionHandle<T> handle)
    {
        if (_count == _entries.Length)
            Array.Resize(ref _entries, _entries.Length * 2);

        int index = _count;
        _entries[index] = new WatchEntry<T>
        {
            EntityId = entityId,
            Sync = sync,
            Handle = handle
        };

        handle.EntryIndex = index;
        _count++;

        return index;
    }

    internal void RemoveEntry(ComponentSubscriptionHandle<T> handle)
    {
        int idx = handle.EntryIndex;
        if (idx < 0 || idx >= _count) return;

        if (_isTicking)
        {
            _entries[idx].Sync = null;
            _pendingRemoves ??= new List<int>();
            _pendingRemoves.Add(idx);
            return;
        }

        SwapRemove(idx);
    }

    private void SwapRemove(int idx)
    {
        int last = _count - 1;
        if (idx < last)
        {
            _entries[idx] = _entries[last];
            if (_entries[idx].Handle != null)
                _entries[idx].Handle!.EntryIndex = idx;
        }

        _entries[last] = default;
        _count--;
    }

    private void ProcessPendingRemoves()
    {
        if (_pendingRemoves == null || _pendingRemoves.Count == 0) return;

        _pendingRemoves.Sort();
        for (int i = _pendingRemoves.Count - 1; i >= 0; i--)
        {
            int idx = _pendingRemoves[i];
            if (idx < _count && _entries[idx].Sync == null)
                SwapRemove(idx);
        }
        _pendingRemoves.Clear();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void CheckSingleEntry(int index)
    {
        if (Owner != null && Owner.IsResimulating) return;

        var store = _store;
        if (store == null || index >= _count) return;

        ref var entry = ref _entries[index];
        if (entry.Sync == null || entry.EntityId >= store.Length) return;

        entry.Sync(store.Get(entry.EntityId));
    }

    public override void CheckAndNotify()
    {
        var store = _store;
        if (store == null || _count == 0) return;

        int stride = _stride;
        int elemSize = _elemSize;
        var buffer = store.GetBufferSpan();
        int bufferLen = buffer.Length;

        // Ensure snapshot is large enough
        if (_snapshot.Length < bufferLen)
        {
            var newSnapshot = new byte[bufferLen];
            if (_hasSnapshot && _snapshotLength > 0)
                Buffer.BlockCopy(_snapshot, 0, newSnapshot, 0, Math.Min(_snapshotLength, newSnapshot.Length));
            _snapshot = newSnapshot;
        }

        if (!_hasSnapshot)
        {
            // First tick: copy full buffer, notify all subscribers
            buffer.CopyTo(_snapshot);
            _snapshotLength = bufferLen;
            _hasSnapshot = true;

            _isTicking = true;
            for (int i = 0; i < _count; i++)
            {
                ref var entry = ref _entries[i];
                if (entry.Sync == null || entry.EntityId >= store.Length) continue;
                try { entry.Sync(store.Get(entry.EntityId)); }
                catch (Exception ex) { ILogger.LogError($"[BatchComponentObserver] Error in sync callback: {ex}"); }
            }
            _isTicking = false;
            ProcessPendingRemoves();
            return;
        }

        int compareLen = Math.Min(bufferLen, _snapshotLength);

        // Fast path: single bulk compare of entire store buffer vs snapshot
        if (buffer.Slice(0, compareLen).SequenceEqual(new ReadOnlySpan<byte>(_snapshot, 0, compareLen)))
        {
            _snapshotLength = bufferLen;
            return;
        }

        // Something changed — scan only subscribed entities via direct entry iteration
        var snap = _snapshot;
        _isTicking = true;

        for (int i = 0; i < _count; i++)
        {
            ref var entry = ref _entries[i];
            if (entry.Sync == null) continue;

            int entityId = entry.EntityId;
            if (entityId >= store.Length) continue;

            int offset = entityId * stride;
            if (offset + elemSize > bufferLen) continue;

            var current = buffer.Slice(offset, elemSize);
            if (current.SequenceEqual(new ReadOnlySpan<byte>(snap, offset, elemSize)))
                continue;

            // Don't update snap mid-loop — multiple entries may watch the same entity,
            // and per-slot snap updates would make later entries see equal-to-snap and
            // skip, silently dropping their sync. The full-buffer CopyTo below covers it.

            try { entry.Sync(store.Get(entityId)); }
            catch (Exception ex) { ILogger.LogError($"[BatchComponentObserver] Error in sync callback: {ex}"); }
        }

        // Copy full buffer to snapshot so unwatched entity changes don't re-trigger the slow path
        buffer.CopyTo(_snapshot);
        _snapshotLength = bufferLen;
        _isTicking = false;
        ProcessPendingRemoves();
    }

    public override void Reset()
    {
        _store = _context.GetComponentStore<T>();
        _elemSize = _store?.ElementSize ?? 0;
        _stride = _store?.Stride ?? 0;
        _hasSnapshot = false;
        CheckAndNotify();
    }

    protected override void OnDispose()
    {
        for (int i = _count - 1; i >= 0; i--)
        {
            if (_entries[i].Handle != null)
            {
                _entries[i].Handle!.Batch = null;
                _entries[i].Handle = null;
            }
        }
        _count = 0;
        _context = null!;
        _store = null;
        _hasSnapshot = false;
    }
}
