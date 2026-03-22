using System.Runtime.CompilerServices;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.ECS;

public class EntityWorld
{
    internal Array?[] _componentArrays = new Array?[128];
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
        for (int i = 0; i < _componentArrays.Length; i++)
        {
            var arr = _componentArrays[i];
            if (arr != null)
            {
                // If the array is too small for the new ID, resize it now
                if (id >= arr.Length)
                {
                    int newSize = Math.Max(arr.Length * 2, id + 1);
                
                    // We need to use a helper or reflection because _componentArrays is Array?[]
                    // Let's create a specialized resize-and-clear helper:
                    _componentArrays[i] = ResizeAndClear(arr, newSize);
                }

                // Now it is safe to clear
                Array.Clear(_componentArrays[i]!, id, 1);
            }
        }

        return new Entity(id);
    }
    
    private Array ResizeAndClear(Array oldArray, int newSize)
    {
        Type elementType = oldArray.GetType().GetElementType()!;
        Array newArray = Array.CreateInstance(elementType, newSize);
    
        // Copy old data
        Array.Copy(oldArray, newArray, oldArray.Length);
    
        // Explicitly zero out the new portion (crucial for determinism!)
        Array.Clear(newArray, oldArray.Length, newSize - oldArray.Length);
    
        return newArray;
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

        // 2. Clear Component Arrays
        if (clearCache)
        {
            // Hard reset: drop everything to force re-resolution of types
            Array.Clear(_componentTypes, 0, _componentTypes.Length);
            Array.Clear(_componentElementSizes, 0, _componentElementSizes.Length);
            Array.Clear(_componentArrays, 0, _componentArrays.Length);
        }
        else
        {
            // Soft reset: keep arrays, just clear content
            for (int i = 0; i < _componentArrays.Length; i++)
            {
                if (_componentArrays[i] != null)
                {
                    Array.Clear(_componentArrays[i]!, 0, _componentArrays[i]!.Length);
                }
            }
        }

        // 3. Reset Entity Allocator?
        // Deserialization sets _nextEntityId, so strictly speaking we don't need to,
        // but it's good practice.
        _nextEntityId = 0;

        // 4. Clear Dirty Tracking
        _dirtyEntitySet.SetAll(false);
        _dirtyEntities.Clear();

        // 5. Clear External State
        ExternalState.Clear();
    }

    public ref T AddComponent<T>(Entity entity, T component) where T : struct, IComponent
    {
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

        // Unset mask
        _entityMasks[entity.Id].Unset(typeId);

        // We don't necessarily need to clear the data array for value types,
        // as the mask determines presence.
        // But for safety/determinism (to avoid stale data if re-added without init),
        // we can default it if we want, or leave it.
        // In ECS, usually mask is the source of truth.
        // Let's clear it to be safe against partial updates on re-add.
        if (typeId < _componentArrays.Length && _componentArrays[typeId] != null)
        {
             var specificArray = _componentArrays[typeId] as T[];
             if (specificArray != null && entity.Id < specificArray.Length)
             {
                 specificArray[entity.Id] = default;
             }
        }
    }

    public void DeleteEntity(Entity entity)
    {
        if (entity.Id >= _entityMasks.Length) return;

        MarkDirty(entity.Id);

        // Clear component data for all components this entity has
        // This is important to release references if components hold any,
        // and to ensure deterministic clean state if the ID is reused (though currently it isn't).
        for (int i = 0; i < 128; i++)
        {
            if (_entityMasks[entity.Id].IsSet(i))
            {
                if (i < _componentArrays.Length && _componentArrays[i] != null)
                {
                    Array.Clear(_componentArrays[i]!, entity.Id, 1);
                }
            }
        }

        // Clear the mask, effectively removing the entity from all queries
        _entityMasks[entity.Id].Clear();
    }

    public ref T GetComponent<T>(Entity entity) where T : struct, IComponent
    {
        var typeId = ComponentId<T>.IntId;

        EnsureTypedCapacity<T>(typeId);
        EnsureEntityCapacity(entity.Id);

        var specificArray = (T[])_componentArrays[typeId]!;

        if (entity.Id >= specificArray.Length)
        {
            ExpandComponentArrayCapacity<T>(typeId, specificArray, entity.Id);
            specificArray = (T[])_componentArrays[typeId]!; // Re-fetch after expansion
        }

        return ref specificArray[entity.Id];
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

        var specificArray = (T[])_componentArrays[typeId]!;

        if (entity.Id >= specificArray.Length)
        {
            ExpandComponentArrayCapacity<T>(typeId, specificArray, entity.Id);
            specificArray = (T[])_componentArrays[typeId]!;
        }

        return ref specificArray[entity.Id];
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

        var specificArray = (T1[])_componentArrays[typeId]!;

        for (int i = 0; i < _entityMasks.Length; i++)
        {
            if (_entityMasks[i].IsSet(typeId))
            {
                action(ref specificArray[i]);
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

        var t1Array = (T1[])_componentArrays[t1Id]!;
        var t2Array = (T2[])_componentArrays[t2Id]!;

        for (int i = 0; i < _entityMasks.Length; i++)
        {
            if (_entityMasks[i].HasAll(mask))
            {
                action(ref t1Array[i], ref t2Array[i]);
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

        var t1Array = (T1[])_componentArrays[t1Id]!;

        for (int i = 0; i < _entityMasks.Length; i++)
        {
            if (_entityMasks[i].HasAll(mask))
            {
                action(new Entity(i), ref t1Array[i]);
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

        var t1Array = (T1[])_componentArrays[t1Id]!;
        var t2Array = (T2[])_componentArrays[t2Id]!;

        for (int i = 0; i < _entityMasks.Length; i++)
        {
            if (_entityMasks[i].HasAll(mask))
            {
                action(new Entity(i), ref t1Array[i], ref t2Array[i]);
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

        var t1Array = (T1[])_componentArrays[t1Id]!;
        var t2Array = (T2[])_componentArrays[t2Id]!;

        for (int i = 0; i < _entityMasks.Length; i++)
        {
            if (_entityMasks[i].HasAll(mask))
            {
                action(state, new Entity(i), ref t1Array[i], ref t2Array[i]);
            }
        }
    }

    public void RegisterComponent<T>() where T : struct, IComponent
    {
        var typeId = ComponentId<T>.IntId;
        EnsureTypedCapacity<T>(typeId);
    }

    public T[] GetRawArray<T>() where T : struct, IComponent
    {
        var typeId = ComponentId<T>.IntId;
        EnsureTypedCapacity<T>(typeId);
        return (T[])_componentArrays[typeId]!;
    }

    internal void EnsureTypedCapacityInternal(int typeId)
    {
        if (typeId >= _componentArrays.Length)
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
                    // Get managed size using reflection to invoke Unsafe.SizeOf<T>()
                    _componentElementSizes[typeId] = GetManagedSize(type);
                }
            }
        }
    }

    internal void EnsureTypedCapacity<T>(int typeId) where T : struct, IComponent
    {
        EnsureTypedCapacityInternal(typeId);

        if (_componentArrays[typeId] == null)
        {
            int initialSize = Math.Max(32, _entityMasks.Length); 
            var arr = new T[initialSize];
            Array.Clear(arr, 0, arr.Length);
            _componentArrays[typeId] = arr;
        
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
        // Fast clear
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
        int newSize = Math.Max(_componentArrays.Length * 2, typeId + 1);
        Array.Resize(ref _componentArrays, newSize);
        Array.Resize(ref _componentElementSizes, newSize);
        Array.Resize(ref _componentTypes, newSize);
    }

    private void ExpandComponentArrayCapacity<T>(int typeId, T[] specificArray, int entityId) where T : struct, IComponent
    {
        int oldSize = specificArray.Length;
        int newSize = Math.Max(oldSize * 2, entityId + 1);
        Array.Resize(ref specificArray, newSize);

        Array.Clear(specificArray, oldSize, newSize - oldSize); // Clear memory from garbage

        _componentArrays[typeId] = specificArray;
    }

    private static int GetManagedSize(Type type)
    {
        var method = typeof(Unsafe).GetMethod("SizeOf")!.MakeGenericMethod(type);
        return (int)method.Invoke(null, null)!;
    }
}
