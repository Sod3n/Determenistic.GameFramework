using System;
using System.Collections;
using System.Collections.Generic;
using Deterministic.GameFramework.CoreV2;

namespace Deterministic.GameFramework.Reactive;

internal class ArchetypeObserver : ObserverNode
{
    private GlobalState _state;
    private BitMask128 _queryMask;
    private Action<Entity> _onAdd;
    private Action<Entity> _onRemove;
    private Func<Entity, bool>? _filter;
    
    // Optimized bitset using ulong[]
    private ulong[] _matchedEntities;
    private int _capacity;

    public ArchetypeObserver()
    {
        _capacity = 256;
        _matchedEntities = new ulong[(_capacity + 63) / 64];
        _state = default!;
        _onAdd = default!;
        _onRemove = default!;
    }

    public void Initialize(GlobalState state, BitMask128 queryMask, Action<Entity> onAdd, Action<Entity> onRemove, Func<Entity, bool>? filter = null)
    {
        _state = state;
        _queryMask = queryMask;
        _onAdd = onAdd;
        _onRemove = onRemove;
        _filter = filter;

        // Reset tracking
        int requiredLength = _state.NextEntityId;
        EnsureCapacity(requiredLength);
        
        // Clear all bits
        Array.Clear(_matchedEntities, 0, _matchedEntities.Length);
        
        // Initial full scan (O(N) only once)
        FullScan();
    }

    public override void CheckAndNotify()
    {
        // 1. Rollback Protection
        if (Owner?.IsResimulating == true) return;

        // 2. Dirty Tracking Optimization
        // Instead of polling all entities, only check those that changed
        var dirtyEntities = _state.GetDirtyEntities();
        if (dirtyEntities.Count == 0) return;

        // Ensure capacity if new entities were added
        if (_state.NextEntityId > _capacity)
        {
             EnsureCapacity(_state.NextEntityId);
        }

        foreach (var entityId in dirtyEntities)
        {
            ProcessEntity(entityId);
        }
    }

    public override void Reset()
    {
        // On reset (e.g. after rollback), we can't trust dirty lists or incremental state.
        // We must re-scan everything to ensure our bitset matches the restored state.
        EnsureCapacity(_state.NextEntityId);
        FullScan();
    }

    private void FullScan()
    {
        int limit = _state.NextEntityId;
        for (int i = 0; i < limit; i++)
        {
            ProcessEntity(i);
        }
    }

    private void ProcessEntity(int id)
    {
        bool isMatch = _state.EntityMasks[id].HasAll(_queryMask);
        
        if (isMatch && _filter != null)
        {
            isMatch = _filter(new Entity(id));
        }
        
        int wordIndex = id >> 6; // id / 64
        int bitIndex = id & 63;  // id % 64
        ulong bitMask = 1UL << bitIndex;
        
        bool wasMatch = (_matchedEntities[wordIndex] & bitMask) != 0;

        if (isMatch != wasMatch)
        {
            if (isMatch)
            {
                _matchedEntities[wordIndex] |= bitMask;
                // Console.WriteLine($"[ArchetypeObserver] Entity {id} Added! (Mask Match)");
                _onAdd?.Invoke(new Entity(id));
            }
            else
            {
                _matchedEntities[wordIndex] &= ~bitMask;
                // Console.WriteLine($"[ArchetypeObserver] Entity {id} Removed!");
                _onRemove?.Invoke(new Entity(id));
            }
        }
    }

    private void EnsureCapacity(int count)
    {
        if (count > _capacity)
        {
            int newCapacity = Math.Max(_capacity * 2, count);
            int newWordCount = (newCapacity + 63) / 64;
            Array.Resize(ref _matchedEntities, newWordCount);
            _capacity = newCapacity;
        }
    }

    protected override void OnDispose()
    {
        _state = null!;
        _onAdd = null!;
        _onRemove = null!;
        _queryMask = default;
        
        // Keep array allocated for pooling, but clear it
        Array.Clear(_matchedEntities, 0, _matchedEntities.Length);
        
        ObserverPool<ArchetypeObserver>.Return(this);
    }
}
