using System.Runtime.CompilerServices;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.ECS;

public class EntityWorld
{
    internal AlignedComponentStore?[] _componentStores = new AlignedComponentStore?[128];
    internal int[] _componentElementSizes = new int[128];
    internal Type?[] _componentTypes = new Type?[128];
    internal BitMask128[] _entityMasks = new BitMask128[256]; // Grows with Entity ID
    internal int _nextEntityId = 0;

    public int NextEntityId => _nextEntityId;
    public BitMask128[] EntityMasks => _entityMasks;

    // Allows systems to store arbitrary state (e.g. Physics World Serialization) that needs to be snapshotted.
    // Key: System Name (e.g. "RapierPhysics"), Value: Serialized Data
    public Dictionary<string, byte[]> ExternalState { get; set; } = new();

    // Runtime cache for Systems (not serialized)
    // Allows systems to be stateless and store per-world data here.
    public Dictionary<Type, object?> SystemData { get; } = new();


    // Dirty tracking
    internal System.Collections.BitArray _dirtyEntitySet = new System.Collections.BitArray(256);
    internal List<int> _dirtyEntities = new List<int>(64);

    public EntityWorld()
    {
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

    public Entity CreateEntity()
    {
        var id = _nextEntityId++;
        EnsureEntityCapacity(id);

        // Clear memory garbage
        for (int i = 0; i < _componentStores.Length; i++)
        {
            var store = _componentStores[i];
            if (store != null)
            {
                if (id >= store.Length)
                {
                    int newSize = Math.Max(store.Length * 2, id + 1);
                    store.Resize(newSize);
                }

                store.Clear(id, 1);
            }
        }

        return new Entity(id);
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

    public bool HasComponent<T>(Entity entity) where T : struct, IComponent
    {
        if (entity.Id < 0 || entity.Id >= _entityMasks.Length) return false;

        var typeId = ComponentId<T>.IntId;
        return _entityMasks[entity.Id].IsSet(typeId);
    }

    public IEnumerable<Entity> Filter<T>() where T : struct, IComponent
    {
        var typeId = ComponentId<T>.IntId;

        for (int i = 0; i < _entityMasks.Length; i++)
        {
            if (_entityMasks[i].IsSet(typeId))
            {
                yield return new Entity(i);
            }
        }
    }

    public IEnumerable<Entity> Filter<T1, T2>()
        where T1 : struct, IComponent
        where T2 : struct, IComponent
    {
        var mask = new BitMask128();
        mask.Set(ComponentId<T1>.IntId);
        mask.Set(ComponentId<T2>.IntId);

        for (int i = 0; i < _entityMasks.Length; i++)
        {
            if (_entityMasks[i].HasAll(mask))
            {
                yield return new Entity(i);
            }
        }
    }

    public IEnumerable<Entity> Filter<T1, T2, T3>()
        where T1 : struct, IComponent
        where T2 : struct, IComponent
        where T3 : struct, IComponent
    {
        var mask = new BitMask128();
        mask.Set(ComponentId<T1>.IntId);
        mask.Set(ComponentId<T2>.IntId);
        mask.Set(ComponentId<T3>.IntId);

        for (int i = 0; i < _entityMasks.Length; i++)
        {
            if (_entityMasks[i].HasAll(mask))
            {
                yield return new Entity(i);
            }
        }
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
    /// Called from deserialization when we only have the runtime Type.
    /// </summary>
    internal void EnsureStoreFromType(int typeId, Type componentType, int capacity)
    {
        if (typeId >= _componentStores.Length)
            ExpandTypeCapacity(typeId);

        if (_componentStores[typeId] == null || _componentStores[typeId]!.Length != capacity)
        {
            var storeType = typeof(AlignedComponentStore<>).MakeGenericType(componentType);
            _componentStores[typeId] = (AlignedComponentStore)Activator.CreateInstance(storeType, capacity)!;
        }
    }

    internal void EnsureTypedCapacity<T>(int typeId) where T : struct, IComponent
    {
        EnsureTypedCapacityInternal(typeId);

        if (_componentStores[typeId] == null)
        {
            int initialSize = Math.Max(32, _entityMasks.Length);
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
        store.Resize(newSize);
    }

    private static int GetManagedSize(Type type)
    {
        var method = typeof(Unsafe).GetMethod("SizeOf")!.MakeGenericMethod(type);
        return (int)method.Invoke(null, null)!;
    }
}
