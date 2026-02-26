using System;
using System.Collections.Generic;

namespace Deterministic.GameFramework.CoreV2;

public class GlobalState
{
    private Array[] _componentArrays = new Array[128];
    private HashSet<int>[] _componentMasks = new HashSet<int>[128];

    public GlobalState()
    {
        InitializeMasks(0, _componentMasks.Length);
    }

    public ref T GetState<T>(Entity entity) where T : struct, IComponent
    {
        var typeId = InternalTypeId<T>.Value;
        
        EnsureTypedCapacity<T>(typeId);

        var specificArray = (T[])_componentArrays[typeId];
        
        if (entity.Id >= specificArray.Length)
        {
            ExpandEntityCapacity<T>(typeId, specificArray, entity.Id);
            specificArray = (T[])_componentArrays[typeId]; // Re-fetch after expansion
        }

        _componentMasks[typeId].Add(entity.Id);

        return ref specificArray[entity.Id];
    }
    
    public bool HasComponent<T>(Entity entity) where T : struct, IComponent
    {
        var typeId = InternalTypeId<T>.Value;
        if (typeId >= _componentMasks.Length) return false;
        return _componentMasks[typeId].Contains(entity.Id);
    }
    
    public T[] GetRawArray<T>() where T : struct, IComponent
    {
        var typeId = InternalTypeId<T>.Value;
        EnsureTypedCapacity<T>(typeId);
        return (T[])_componentArrays[typeId];
    }

    internal void EnsureTypedCapacity<T>(int typeId) where T : struct, IComponent
    {
        if (typeId >= _componentArrays.Length)
        {
            ExpandTypeCapacity(typeId);
        }

        if (_componentArrays[typeId] == null)
        {
            _componentArrays[typeId] = new T[256];
        }
    }

    public void Execute<TAction>(TAction action, Entity entity, Dispatcher dispatcher) where TAction : struct, IAction 
    {
        dispatcher.Execute(action, this, entity);
    }

    private void InitializeMasks(int startIndex, int endIndex)
    {
        for (int i = startIndex; i < endIndex; i++)
        {
            _componentMasks[i] = new HashSet<int>();
        }
    }

    private void ExpandTypeCapacity(int typeId)
    {
        int newSize = Math.Max(_componentArrays.Length * 2, typeId + 1);
        
        Array.Resize(ref _componentArrays, newSize);
        
        var oldLength = _componentMasks.Length;
        Array.Resize(ref _componentMasks, newSize);
        InitializeMasks(oldLength, newSize);
    }

    private void ExpandEntityCapacity<T>(int typeId, T[] specificArray, int entityId) where T : struct, IComponent
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
