using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Deterministic.GameFramework.CoreV2;

public class GlobalState
{
    internal Array[] _componentArrays = new Array[128];
    internal int[] _componentElementSizes = new int[128];
    internal Type[] _componentTypes = new Type[128];
    internal BitMask128[] _entityMasks = new BitMask128[256]; // Grows with Entity ID
    internal int _nextEntityId = 0;
    
    // Dirty tracking
    internal System.Collections.BitArray _dirtyEntitySet = new System.Collections.BitArray(256);
    internal List<int> _dirtyEntities = new List<int>(64);

    public GlobalState()
    {
    }

    public Entity CreateEntity()
    {
        var id = _nextEntityId++;
        EnsureEntityCapacity(id);
        return new Entity(id);
    }

    public void AddComponent<T>(Entity entity, T component) where T : struct, IComponent
    {
        MarkDirty(entity.Id);
        ref var storage = ref GetState<T>(entity);
        storage = component;
    }

    public void RemoveComponent<T>(Entity entity) where T : struct, IComponent
    {
        if (entity.Id >= _entityMasks.Length) return;
        
        MarkDirty(entity.Id);
        var typeId = InternalTypeId<T>.Value;
        
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
             var specificArray = (T[])_componentArrays[typeId];
             if (entity.Id < specificArray.Length)
             {
                 specificArray[entity.Id] = default;
             }
        }
    }

    public ref T GetState<T>(Entity entity) where T : struct, IComponent
    {
        var typeId = InternalTypeId<T>.Value;
        
        EnsureTypedCapacity<T>(typeId);
        EnsureEntityCapacity(entity.Id);

        var specificArray = (T[])_componentArrays[typeId];
        
        if (entity.Id >= specificArray.Length)
        {
            ExpandComponentArrayCapacity<T>(typeId, specificArray, entity.Id);
            specificArray = (T[])_componentArrays[typeId]; // Re-fetch after expansion
        }

        // Mark component as present using bitmask (Fast!)
        _entityMasks[entity.Id].Set(typeId);
        Console.WriteLine($"[GlobalState] Entity {entity.Id} Set Component {typeof(T).Name} (ID {typeId}). IsSet: {_entityMasks[entity.Id].IsSet(typeId)}");

        return ref specificArray[entity.Id];
    }
    
    public bool HasComponent<T>(Entity entity) where T : struct, IComponent
    {
        if (entity.Id >= _entityMasks.Length) return false;
        
        var typeId = InternalTypeId<T>.Value;
        return _entityMasks[entity.Id].IsSet(typeId);
    }
    
    // Example of a fast filter query using BitMasks
    public IEnumerable<Entity> Filter<T1, T2>() 
        where T1 : struct, IComponent 
        where T2 : struct, IComponent
    {
        var mask = new BitMask128();
        mask.Set(InternalTypeId<T1>.Value);
        mask.Set(InternalTypeId<T2>.Value);
        
        for (int i = 0; i < _entityMasks.Length; i++)
        {
            if (_entityMasks[i].HasAll(mask))
            {
                yield return new Entity(i);
            }
        }
    }
    
    public void RegisterComponent<T>() where T : struct, IComponent
    {
        var typeId = InternalTypeId<T>.Value;
        EnsureTypedCapacity<T>(typeId);
    }

    public T[] GetRawArray<T>() where T : struct, IComponent
    {
        var typeId = InternalTypeId<T>.Value;
        EnsureTypedCapacity<T>(typeId);
        return (T[])_componentArrays[typeId];
    }

    internal void EnsureTypedCapacityInternal(int typeId)
    {
         if (typeId >= _componentArrays.Length)
        {
            ExpandTypeCapacity(typeId);
        }
    }

    internal void EnsureTypedCapacity<T>(int typeId) where T : struct, IComponent
    {
        EnsureTypedCapacityInternal(typeId);

        if (_componentArrays[typeId] == null)
        {
            _componentArrays[typeId] = new T[256];
            _componentElementSizes[typeId] = System.Runtime.InteropServices.Marshal.SizeOf<T>();
            _componentTypes[typeId] = typeof(T);
        }
    }
    
    internal void EnsureEntityCapacity(int entityId)
    {
        if (entityId >= _entityMasks.Length)
        {
            int newSize = Math.Max(_entityMasks.Length * 2, entityId + 1);
            Array.Resize(ref _entityMasks, newSize);
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

    public void Execute<TAction>(TAction action, Entity entity, Dispatcher dispatcher) where TAction : struct, IAction 
    {
        dispatcher.Execute(action, this, entity);
    }

    private void ExpandTypeCapacity(int typeId)
    {
        int newSize = Math.Max(_componentArrays.Length * 2, typeId + 1);
        Array.Resize(ref _componentArrays, newSize);
        Array.Resize(ref _componentElementSizes, newSize);
        Array.Resize(ref _componentTypes, newSize);
    }

    private void ExpandComponentArrayCapacity<T>(int typeId, T[] specificArray, int entityId) where T : struct, IComponent
    {
        Array.Resize(ref specificArray, Math.Max(specificArray.Length * 2, entityId + 1));
        _componentArrays[typeId] = specificArray;
    }
}

internal static class InternalTypeId<T> where T : struct, IComponent
{
    public static readonly int Value = GetId();

    private static int GetId()
    {
        // Slow reflection path, but only runs ONCE per type per application lifetime.
        // This is acceptable for initialization.
        var attr = typeof(T).GetCustomAttributes(typeof(NetworkIdAttribute), false);
        if (attr.Length > 0)
        {
            return ((NetworkIdAttribute)attr[0]).Id;
        }
        
        throw new Exception($"Component {typeof(T).Name} is missing [NetworkId] attribute. All components must have a fixed ID for determinism.");
    }
}
