using System.Collections;
using System.Collections.Generic;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Reactive;

internal class ArchetypeObserver : ObserverNode
{
    private EntityWorld _state;
    private BitMask128 _queryMask;
    private Action<Entity> _onAdd;
    private Action<Entity> _onRemove;
    private Func<Entity, bool>? _filter;

    // Component types used to build the query mask.
    // Stored so the mask can be rebuilt during Reset() if LocalIds changed
    // (e.g. after AdoptMappingsFrom during state sync).
    private Type[] _componentTypes = Array.Empty<Type>();

    // Optimized bitset using ulong[]
    private ulong[] _matchedEntities;
    private int _capacity;
    private bool _isDisposed;

    // Pending removes — buffered until confirmed by Reset().
    // During normal ticking, entities that stop matching are put here
    // instead of immediately firing _onRemove. This prevents visual
    // flickering from temporary entity mask changes during prediction.
    // Confirmed removes fire during Reset() (after rollback, state sync,
    // or verified hash match).
    private readonly List<int> _pendingRemoves = new();

    public ArchetypeObserver()
    {
        _capacity = 256;
        _matchedEntities = new ulong[(_capacity + 63) / 64];
        _state = default!;
        _onAdd = default!;
        _onRemove = default!;
    }

    public void Initialize(EntityWorld state, BitMask128 queryMask, Action<Entity> onAdd, Action<Entity> onRemove, Func<Entity, bool>? filter = null, Type[]? componentTypes = null)
    {
        _state = state;
        _queryMask = queryMask;
        _onAdd = onAdd;
        _onRemove = onRemove;
        _filter = filter;
        _componentTypes = componentTypes ?? Array.Empty<Type>();
        _isDisposed = false;

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
        // Rebuild query mask from stored component types in case LocalIds
        // changed (e.g. after AdoptMappingsFrom during state sync).
        if (_componentTypes.Length > 0)
        {
            _queryMask = new BitMask128();
            foreach (var type in _componentTypes)
            {
                try
                {
                    var id = ComponentId.FromType(type);
                    _queryMask.Set(id.ToDense().Value);
                }
                catch { /* Type not registered — skip */ }
            }
        }

        // Re-scan everything to ensure our bitset matches the restored state.
        EnsureCapacity(_state.NextEntityId);
        FullScan();
    }

    private void FullScan()
    {
        int limit = _state.NextEntityId;
        
        // 1. Scan valid entities
        for (int i = 0; i < limit; i++)
        {
            ProcessEntity(i);
        }

        // 2. Clear any bits set beyond the current limit (Ghost entities from future state)
        // These entities existed in the "future" (before rollback) but don't exist now.
        // We must consider them removed.
        
        int totalBits = _matchedEntities.Length * 64;
        for (int i = limit; i < totalBits; i++)
        {
            int wordIndex = i >> 6;
            ulong bitMask = 1UL << (i & 63);
            
            if ((_matchedEntities[wordIndex] & bitMask) != 0)
            {
                // Was matched, now gone
                _matchedEntities[wordIndex] &= ~bitMask;
                // Console.WriteLine($"[ArchetypeObserver] Ghost Entity {i} Removed!");
                _onRemove?.Invoke(new Entity(i));
            }
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
        if (_isDisposed) return;
        _isDisposed = true;

        _state = null!;
        _onAdd = null!;
        _onRemove = null!;
        _queryMask = default;
        
        // Keep array allocated for pooling, but clear it
        Array.Clear(_matchedEntities, 0, _matchedEntities.Length);
        
        ObserverPool<ArchetypeObserver>.Return(this);
    }
}
