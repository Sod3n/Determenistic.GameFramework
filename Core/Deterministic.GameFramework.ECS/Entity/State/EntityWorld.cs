using System.Runtime.CompilerServices;
using Deterministic.GameFramework.Types;
using Deterministic.GameFramework.Utils.Logging;

namespace Deterministic.GameFramework.ECS;

public class EntityWorld
{
    internal AlignedComponentStore?[] _componentStores = new AlignedComponentStore?[128];
    internal int[] _componentElementSizes = new int[128];
    internal Type?[] _componentTypes = new Type?[128];
    internal BitMask128[] _entityMasks = new BitMask128[256]; // Grows with Entity ID
    internal int _nextEntityId = 0;
    private int _reservedCapacity = 0; // Set by ReserveEntityCapacity; used as minimum for new stores

    public int NextEntityId => _nextEntityId;
    public BitMask128[] EntityMasks => _entityMasks;

    /// <summary>
    /// Incremented whenever a component store is resized (CreateEntity, AddComponent
    /// that exceeds store capacity). Any 'ref T' from GetComponent obtained before
    /// this version is stale.
    /// Usage: save version before entity-creating calls, assert unchanged after.
    /// </summary>
    public int StructuralVersion { get; private set; }

    // Allows systems to store arbitrary state (e.g. Physics World Serialization) that needs to be snapshotted.
    // Key: System Name (e.g. "RapierPhysics"), Value: Serialized Data
    public Dictionary<string, byte[]> ExternalState { get; set; } = new();

    // Runtime cache for Systems (not serialized)
    // Allows systems to be stateless and store per-world data here.
    public Dictionary<Type, object?> SystemData { get; } = new();


    // Dirty tracking. Pre-sized generously so steady-state ticks don't trigger internal
    // buffer growth (List<T>.AddWithResize was a top-5 per-tick allocator in the VPS trace).
    internal System.Collections.BitArray _dirtyEntitySet = new System.Collections.BitArray(1024);
    internal List<int> _dirtyEntities = new List<int>(1024);

    // ── Client-side prediction plumbing ───────────────────────────────────────────────
    // Entity ids come from the single _nextEntityId counter — both normal and predicted.
    // The dense _entityMasks array can't tolerate a sparse high-bit id range without
    // ballooning to 2 GB, so we instead let the client allocate predicted entities at
    // regular sequential ids and correlate them via the PredictedByAction(PredictionId)
    // tag auto-attached by CreateEntity when CurrentActionPredictionId != 0.
    //
    // When the server's authoritative delta arrives carrying an entity tagged with the
    // same PredictionId, DeltaSyncStrategyClient maps client-predicted → server-authoritative
    // via its _predictedEntitiesByPid map and destroys the predicted ghost (unless the
    // ids happen to already match, in which case it's a no-op).

    /// <summary>
    /// Set by <c>ActionScheduler.ExecuteActions</c> before invoking an action service, cleared
    /// in <c>finally</c> afterwards. When non-zero, every <see cref="CreateEntity()"/> call
    /// during that service execution auto-attaches a <see cref="PredictedByAction"/> component
    /// carrying this id. Zero (the default) means "not predicting" — no tag.
    /// </summary>
    public long CurrentActionPredictionId;

    /// <summary>
    /// Fired from <see cref="CreateEntity()"/> after the entity id has been allocated and
    /// initial components (including <see cref="PredictedByAction"/> when applicable) have
    /// been attached. Consumers (like <c>DeltaSyncStrategyClient</c>) use this to record
    /// the predicted-entity → action-prediction-id mapping needed for later correlation.
    /// </summary>
    public event Action<Entity>? OnEntityCreated;

    public EntityWorld(int reserveCapacity = 0)
    {
        if (reserveCapacity > 0)
            _reservedCapacity = reserveCapacity;
        var worldEntity = CreateEntity();
        AddComponent(worldEntity, new World());
    }

    public void SetCustomData<T>(T? data) => SystemData[typeof(T)] = data;
    public T? GetCustomData<T>()
    {
        if (SystemData.TryGetValue(typeof(T), out var data))
        {
            return (T?)data;
        }
        return default;
    }

    public void ClearCustomData()
    {
        foreach (var value in SystemData.Values)
        {
            if (value is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        SystemData.Clear();
    }

    /// <summary>
    /// Invalidates derived/cached state in SystemData after a state restore
    /// (rollback or full state sync). Determinism-critical flags live in ECS
    /// components (serialized), but derived objects (physics worlds, nav meshes,
    /// crowd sims) must be rebuilt from the restored ECS state.
    ///
    /// Each system's derived state implements <see cref="IDerivedState"/> to
    /// define its own invalidation logic. Systems that don't implement the
    /// interface are left untouched.
    /// </summary>
    public void InvalidateDerivedState(bool full = false)
    {
        foreach (var kvp in SystemData)
        {
            if (kvp.Value is IDerivedState derived)
                derived.Invalidate(full);
        }
    }

    /// <summary>
    /// Pre-allocates entity masks and all existing component stores to hold at least
    /// <paramref name="capacity"/> entities. Call this early (e.g. in GameFactory) to
    /// avoid store resizes during gameplay — each resize invalidates every outstanding
    /// 'ref T' from GetComponent().
    /// </summary>
    public void ReserveEntityCapacity(int capacity)
    {
        _reservedCapacity = Math.Max(_reservedCapacity, capacity);

        if (capacity > _entityMasks.Length)
        {
            int oldSize = _entityMasks.Length;
            Array.Resize(ref _entityMasks, capacity);
            for (int i = oldSize; i < capacity; i++) _entityMasks[i].Clear();
            _dirtyEntitySet.Length = capacity;
        }

        for (int i = 0; i < _componentStores.Length; i++)
        {
            var store = _componentStores[i];
            if (store != null && store.Length < capacity)
                store.Resize(capacity);
        }
    }

    public Entity CreateEntity()
    {
        // Predicted entities share the normal id sequence. The PredictedByAction tag attached
        // below is what lets DeltaSyncStrategyClient correlate them with the server's
        // authoritative ids later — we don't need a separate id space, and using a sparse
        // high-bit range would explode _entityMasks' memory footprint.
        bool predicted = CurrentActionPredictionId != 0;
        int id = _nextEntityId++;

        EnsureEntityCapacity(id);

        // Clear memory garbage
        bool resized = false;
        for (int i = 0; i < _componentStores.Length; i++)
        {
            var store = _componentStores[i];
            if (store != null)
            {
                if (id >= store.Length)
                {
                    int newSize = Math.Max(store.Length * 2, id + 1);
                    store.Resize(newSize);
                    resized = true;
                }

                store.Clear(id, 1);
            }
        }

        if (resized)
        {
            ILogger.LogWarning($"[EntityWorld] Store resize at entity {id}. " +
                $"All ref T from GetComponent() before this point are stale. " +
                $"Consider increasing ReserveEntityCapacity.");
            StructuralVersion++;
        }

        var entity = new Entity(id);

        // Auto-tag predicted entities so the delta applier can correlate later. We do this
        // BEFORE firing OnEntityCreated so subscribers see the fully-formed (tagged) entity.
        if (predicted)
        {
            AddComponent(entity, new PredictedByAction { PredictionId = CurrentActionPredictionId });
        }

        OnEntityCreated?.Invoke(entity);
        return entity;
    }

    public Entity CreateEntity<T>() where T : struct, IComponent
    {
        var entity = CreateEntity();
        AddComponent(entity, default(T));
        return entity;
    }

    public void ResetComponents(bool clearCache = false)
    {
        // 1. Clear Entity Masks
        for (int i = 0; i < _entityMasks.Length; i++)
        {
            _entityMasks[i].Clear();
        }

        // 2. Clear Component Stores
        if (clearCache)
        {
            Array.Clear(_componentTypes, 0, _componentTypes.Length);
            Array.Clear(_componentElementSizes, 0, _componentElementSizes.Length);
            Array.Clear(_componentStores, 0, _componentStores.Length);
        }
        else
        {
            for (int i = 0; i < _componentStores.Length; i++)
            {
                var store = _componentStores[i];
                if (store != null)
                {
                    store.Clear(0, store.Length);
                }
            }
        }

        // 3. Reset Entity Allocator
        _nextEntityId = 0;

        // 4. Clear Dirty Tracking
        _dirtyEntitySet.SetAll(false);
        _dirtyEntities.Clear();

        // 5. Clear External State
        ExternalState.Clear();
    }

    public ref T AddComponent<T>(Entity entity, T component) where T : struct, IComponent
    {
        EnsureEntityCapacity(entity.Id);
        MarkDirty(entity.Id);
        var typeId = ComponentId<T>.IntId;
        _entityMasks[entity.Id].Set(typeId);
        ref var storage = ref GetComponent<T>(entity);
        storage = component;
        return ref storage;
    }

    public void RemoveComponent<T>(Entity entity) where T : struct, IComponent
    {
        if (entity.Id >= _entityMasks.Length) return;

        MarkDirty(entity.Id);
        var typeId = ComponentId<T>.IntId;

        _entityMasks[entity.Id].Unset(typeId);

        if (typeId < _componentStores.Length && _componentStores[typeId] is AlignedComponentStore<T> store && entity.Id < store.Length)
        {
            store.Get(entity.Id) = default;
        }
    }

    public void DeleteEntity(Entity entity)
    {
        if (entity.Id >= _entityMasks.Length) return;

        MarkDirty(entity.Id);

        for (int i = 0; i < 128; i++)
        {
            if (_entityMasks[entity.Id].IsSet(i))
            {
                if (i < _componentStores.Length && _componentStores[i] != null)
                {
                    _componentStores[i]!.Clear(entity.Id, 1);
                }
            }
        }

        _entityMasks[entity.Id].Clear();
    }

    public ref T GetComponent<T>(Entity entity) where T : struct, IComponent
    {
        var typeId = ComponentId<T>.IntId;

        EnsureTypedCapacity<T>(typeId);
        EnsureEntityCapacity(entity.Id);

        var store = (AlignedComponentStore<T>)_componentStores[typeId]!;

        if (entity.Id >= store.Length)
        {
            ExpandStoreCapacity<T>(typeId, store, entity.Id);
            store = (AlignedComponentStore<T>)_componentStores[typeId]!;
        }

        return ref store.Get(entity.Id);
    }

    /// <summary>
    /// Returns a reference to the component storage without marking it as present in the mask.
    /// Use this for safe reading in selectors or when the presence is already verified.
    /// </summary>
    public ref T PeekComponent<T>(Entity entity) where T : struct, IComponent
    {
        var typeId = ComponentId<T>.IntId;

        EnsureTypedCapacity<T>(typeId);
        EnsureEntityCapacity(entity.Id);

        var store = (AlignedComponentStore<T>)_componentStores[typeId]!;

        if (entity.Id >= store.Length)
        {
            ExpandStoreCapacity<T>(typeId, store, entity.Id);
            store = (AlignedComponentStore<T>)_componentStores[typeId]!;
        }

        return ref store.Get(entity.Id);
    }

    public T? TryGetComponent<T>(Entity entity) where T : struct, IComponent
    {
        if (!HasComponent<T>(entity)) return null;
        return GetComponent<T>(entity);
    }

    public bool TryGetComponent<T>(Entity entity, out T component) where T : struct, IComponent
    {
        if (!HasComponent<T>(entity))
        {
            component = default;
            return false;
        }
        component = GetComponent<T>(entity);
        return true;
    }

    public bool HasComponent<T>(Entity entity) where T : struct, IComponent
    {
        if (entity.Id < 0 || entity.Id >= _entityMasks.Length) return false;

        var typeId = ComponentId<T>.IntId;
        return _entityMasks[entity.Id].IsSet(typeId);
    }

    public MaskFilter Filter<T>() where T : struct, IComponent
    {
        var mask = new BitMask128();
        mask.Set(ComponentId<T>.IntId);
        return new MaskFilter(_entityMasks, mask, _nextEntityId);
    }

    public MaskFilter Filter<T1, T2>()
        where T1 : struct, IComponent
        where T2 : struct, IComponent
    {
        var mask = new BitMask128();
        mask.Set(ComponentId<T1>.IntId);
        mask.Set(ComponentId<T2>.IntId);
        return new MaskFilter(_entityMasks, mask, _nextEntityId);
    }

    public MaskFilter Filter<T1, T2, T3>()
        where T1 : struct, IComponent
        where T2 : struct, IComponent
        where T3 : struct, IComponent
    {
        var mask = new BitMask128();
        mask.Set(ComponentId<T1>.IntId);
        mask.Set(ComponentId<T2>.IntId);
        mask.Set(ComponentId<T3>.IntId);
        return new MaskFilter(_entityMasks, mask, _nextEntityId);
    }



    public void ForEach<T1>(ComponentAction1<T1> action)
        where T1 : struct, IComponent
    {
        var typeId = ComponentId<T1>.IntId;

        EnsureTypedCapacity<T1>(typeId);

        var store = (AlignedComponentStore<T1>)_componentStores[typeId]!;

        for (int i = 0; i < _entityMasks.Length; i++)
        {
            if (_entityMasks[i].IsSet(typeId))
            {
                action(ref store.Get(i));
            }
        }
    }

    public void ForEach<T1, T2>(ComponentAction2<T1, T2> action)
        where T1 : struct, IComponent
        where T2 : struct, IComponent
    {
        var mask = new BitMask128();
        var t1Id = ComponentId<T1>.IntId;
        var t2Id = ComponentId<T2>.IntId;
        mask.Set(t1Id);
        mask.Set(t2Id);

        EnsureTypedCapacity<T1>(t1Id);
        EnsureTypedCapacity<T2>(t2Id);

        var store1 = (AlignedComponentStore<T1>)_componentStores[t1Id]!;
        var store2 = (AlignedComponentStore<T2>)_componentStores[t2Id]!;

        for (int i = 0; i < _entityMasks.Length; i++)
        {
            if (_entityMasks[i].HasAll(mask))
            {
                action(ref store1.Get(i), ref store2.Get(i));
            }
        }
    }

    public void ForEach<T1>(ComponentActionEntity1<T1> action)
        where T1 : struct, IComponent
    {
        var mask = new BitMask128();
        var t1Id = ComponentId<T1>.IntId;
        mask.Set(t1Id);

        EnsureTypedCapacity<T1>(t1Id);

        var store = (AlignedComponentStore<T1>)_componentStores[t1Id]!;

        for (int i = 0; i < _entityMasks.Length; i++)
        {
            if (_entityMasks[i].HasAll(mask))
            {
                action(new Entity(i), ref store.Get(i));
            }
        }
    }

    public void ForEach<T1, T2>(ComponentActionEntity2<T1, T2> action)
        where T1 : struct, IComponent
        where T2 : struct, IComponent
    {
        var mask = new BitMask128();
        var t1Id = ComponentId<T1>.IntId;
        var t2Id = ComponentId<T2>.IntId;
        mask.Set(t1Id);
        mask.Set(t2Id);

        EnsureTypedCapacity<T1>(t1Id);
        EnsureTypedCapacity<T2>(t2Id);

        var store1 = (AlignedComponentStore<T1>)_componentStores[t1Id]!;
        var store2 = (AlignedComponentStore<T2>)_componentStores[t2Id]!;

        for (int i = 0; i < _entityMasks.Length; i++)
        {
            if (_entityMasks[i].HasAll(mask))
            {
                action(new Entity(i), ref store1.Get(i), ref store2.Get(i));
            }
        }
    }

    public void ForEach<T1, T2, TState>(TState state, ComponentActionEntity2<T1, T2, TState> action)
        where T1 : struct, IComponent
        where T2 : struct, IComponent
    {
        var mask = new BitMask128();
        var t1Id = ComponentId<T1>.IntId;
        var t2Id = ComponentId<T2>.IntId;
        mask.Set(t1Id);
        mask.Set(t2Id);

        EnsureTypedCapacity<T1>(t1Id);
        EnsureTypedCapacity<T2>(t2Id);

        var store1 = (AlignedComponentStore<T1>)_componentStores[t1Id]!;
        var store2 = (AlignedComponentStore<T2>)_componentStores[t2Id]!;

        for (int i = 0; i < _entityMasks.Length; i++)
        {
            if (_entityMasks[i].HasAll(mask))
            {
                action(state, new Entity(i), ref store1.Get(i), ref store2.Get(i));
            }
        }
    }

    public void RegisterComponent<T>() where T : struct, IComponent
    {
        var typeId = ComponentId<T>.IntId;
        EnsureTypedCapacity<T>(typeId);
    }

    public AlignedComponentStore<T> GetComponentStore<T>() where T : struct, IComponent
    {
        var typeId = ComponentId<T>.IntId;
        EnsureTypedCapacity<T>(typeId);
        return (AlignedComponentStore<T>)_componentStores[typeId]!;
    }

    /// <summary>
    /// Non-generic component store accessor by dense id. Returns null if the store doesn't
    /// exist yet (e.g. component type never registered). Used by delta generators that iterate
    /// by bit index rather than by generic type.
    /// </summary>
    public AlignedComponentStore? GetComponentStore(int denseId)
    {
        if (denseId < 0 || denseId >= _componentStores.Length) return null;
        return _componentStores[denseId];
    }

    /// <summary>Element size (sans alignment padding) for a dense component id. 0 when unregistered.</summary>
    public int GetComponentElementSize(int denseId)
    {
        if (denseId < 0 || denseId >= _componentElementSizes.Length) return 0;
        return _componentElementSizes[denseId];
    }

    /// <summary>Runtime type for a dense component id, or null when unregistered.</summary>
    public Type? GetComponentType(int denseId)
    {
        if (denseId < 0 || denseId >= _componentTypes.Length) return null;
        return _componentTypes[denseId];
    }

    internal void EnsureTypedCapacityInternal(int typeId)
    {
        if (typeId >= _componentStores.Length)
        {
            ExpandTypeCapacity(typeId);
        }

        if (_componentTypes[typeId] == null)
        {
            if (ComponentId.TryGetType(new DenseComponentId(typeId), out var type))
            {
                if (type != null)
                {
                    _componentTypes[typeId] = type;
                    _componentElementSizes[typeId] = GetManagedSize(type);
                }
            }
        }
    }

    /// <summary>
    /// Creates an AlignedComponentStore for the given type ID if not already present.
    /// Called from deserialization (and delta apply) when we only have the runtime Type.
    /// Also populates <see cref="_componentTypes"/> and <see cref="_componentElementSizes"/>
    /// so serialization does not skip the component.
    /// </summary>
    internal void EnsureStoreFromType(int typeId, Type componentType, int capacity)
    {
        if (typeId >= _componentStores.Length)
            ExpandTypeCapacity(typeId);

        int effectiveCapacity = Math.Max(capacity, _reservedCapacity);
        if (_componentStores[typeId] == null || _componentStores[typeId]!.Length < effectiveCapacity)
        {
            var storeType = typeof(AlignedComponentStore<>).MakeGenericType(componentType);
            _componentStores[typeId] = (AlignedComponentStore)Activator.CreateInstance(storeType, effectiveCapacity)!;
        }

        if (_componentTypes[typeId] == null)
            _componentTypes[typeId] = componentType;

        if (_componentElementSizes[typeId] == 0)
            _componentElementSizes[typeId] = GetManagedSize(componentType);
    }

    internal void EnsureTypedCapacity<T>(int typeId) where T : struct, IComponent
    {
        EnsureTypedCapacityInternal(typeId);

        if (_componentStores[typeId] == null)
        {
            int initialSize = Math.Max(32, Math.Max(_entityMasks.Length, _reservedCapacity));
            _componentStores[typeId] = new AlignedComponentStore<T>(initialSize);
            _componentElementSizes[typeId] = Unsafe.SizeOf<T>();
            _componentTypes[typeId] = typeof(T);
        }
    }

    internal void EnsureEntityCapacity(int entityId)
    {
        if (entityId >= _entityMasks.Length)
        {
            int oldSize = _entityMasks.Length;
            int newSize = Math.Max(oldSize * 2, entityId + 1);
            Array.Resize(ref _entityMasks, newSize);

            // Clear memory garbage
            for(int i = oldSize; i < newSize; i++) _entityMasks[i].Clear();

            _dirtyEntitySet.Length = newSize;
        }
    }

    private void MarkDirty(int entityId)
    {
        if (entityId < _dirtyEntitySet.Length && !_dirtyEntitySet[entityId])
        {
            _dirtyEntitySet[entityId] = true;
            _dirtyEntities.Add(entityId);
        }
    }

    /// <summary>
    /// Mark all entities as dirty so observers re-check them on the next tick.
    /// Called after state deserialization to ensure reactive observers pick up
    /// entity mask changes that weren't tracked incrementally.
    /// </summary>
    public void MarkAllDirty()
    {
        for (int i = 0; i < _nextEntityId; i++)
            MarkDirty(i);
    }

    public void ClearDirty()
    {
        foreach (var id in _dirtyEntities)
        {
            if (id < _dirtyEntitySet.Length)
                _dirtyEntitySet[id] = false;
        }
        _dirtyEntities.Clear();
    }

    public IReadOnlyList<int> GetDirtyEntities() => _dirtyEntities;

    private void ExpandTypeCapacity(int typeId)
    {
        int newSize = Math.Max(_componentStores.Length * 2, typeId + 1);
        Array.Resize(ref _componentStores, newSize);
        Array.Resize(ref _componentElementSizes, newSize);
        Array.Resize(ref _componentTypes, newSize);
    }

    private void ExpandStoreCapacity<T>(int typeId, AlignedComponentStore<T> store, int entityId) where T : struct, IComponent
    {
        int newSize = Math.Max(store.Length * 2, entityId + 1);
        ILogger.LogWarning($"[EntityWorld] Store<{typeof(T).Name}> resize {store.Length}→{newSize} for entity {entityId}. " +
            $"Consider increasing ReserveEntityCapacity.");
        store.Resize(newSize);
        StructuralVersion++;
    }

    private static int GetManagedSize(Type type)
    {
        var method = typeof(Unsafe).GetMethod("SizeOf")!.MakeGenericMethod(type);
        return (int)method.Invoke(null, null)!;
    }
}
