using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

namespace Deterministic.GameFramework.ECS;

public record struct ComponentId : IComparable<ComponentId>
{
    private static int _nextLocalId = 0;
    private static readonly object _registrationLock = new();
    
    // Internal lookup tables
    internal static readonly ConcurrentDictionary<StableComponentId, DenseComponentId> StableToDense = new();
    internal static readonly ConcurrentDictionary<StableComponentId, Type> StableToType = new();
    
    internal static readonly ConcurrentDictionary<DenseComponentId, StableComponentId> DenseToStable = new();
    internal static readonly ConcurrentDictionary<Type, StableComponentId> TypeToStable = new();
    
    internal static readonly ConcurrentDictionary<Type, Action<StableComponentId, DenseComponentId>> TypeToUpdateCache = new();

    private int _localValue;
    private Guid _stableValue;
    private Type? _componentType;
    
    public static void RegisterCacheUpdate(Type type, Action<StableComponentId, DenseComponentId> updateAction)
    {
        TypeToUpdateCache[type] = updateAction;
    }
    
    public static ComponentId FromDense(int value)
    {
        if (!DenseToStable.TryGetValue(new DenseComponentId(value), out var stableId))
        {
            throw new Exception($"LocalId {value} not registered. Call RegisterAssembly first.");
        }
        return FromStable(stableId);
    }

    public static bool TryFromDense(int value, out ComponentId id)
    {
        if (DenseToStable.TryGetValue(new DenseComponentId(value), out var stableId))
        {
            try 
            {
                id = FromStable(stableId);
                return true;
            }
            catch
            {
                // Fallback if inconsistencies exist
            }
        }
        id = default;
        return false;
    }
    
    public static ComponentId FromStable(Guid value)
    {
        var stableId = new StableComponentId(value);
        if (!StableToType.TryGetValue(stableId, out var type))
        {
            throw new Exception($"StableId {stableId} not registered. Call RegisterAssembly first.");
        }
        
        if (!StableToDense.TryGetValue(stableId, out var localId))
        {
            throw new Exception($"StableId {stableId} not registered as local. Call RegisterAssembly first.");
        }
        
        var componentId = new ComponentId
        {
            _stableValue = value,
            _localValue = localId.Value,
            _componentType = type
        };
        
        return componentId;
    }

    public static ComponentId FromType<T>() where T : struct, IComponent
    {
        if (!TypeToStable.TryGetValue(typeof(T), out var stableId))
        {
             throw new Exception($"Type {typeof(T).Name} not registered. Call RegisterAssembly first.");
        }
        return FromStable(stableId);
    }
    
    public static ComponentId FromType(Type type)
    {
        if (!TypeToStable.TryGetValue(type, out var stableId))
        {
             throw new Exception($"Type {type.Name} not registered. Call RegisterAssembly first.");
        }
        return FromStable(stableId);
    }
    
    public DenseComponentId ToDense()
    {
        return new DenseComponentId(_localValue);
    }
    
    public StableComponentId ToStable()
    {
        return new StableComponentId(_stableValue);
    }

    public Type ToType() => _componentType!;
    
    
    public static void RegisterAssembly(Assembly assembly)
    {
        // Collect all IComponent types with StableId, then sort by StableId
        // to ensure deterministic LocalId assignment regardless of GetTypes() order
        // (which can vary across .NET runtimes — e.g. Godot mono vs server .NET 8)
        var components = new List<(Type type, Guid stableId)>();

        foreach (var type in assembly.GetTypes())
        {
            if (!type.IsValueType || !typeof(IComponent).IsAssignableFrom(type)) continue;

            var attr = type.GetCustomAttribute<StableIdAttribute>();
            if (attr == null) continue;

            components.Add((type, attr.Id));
        }

        components.Sort((a, b) => a.stableId.CompareTo(b.stableId));

        foreach (var (type, stableId) in components)
        {
            RegisterType(type, new StableComponentId(stableId));
        }
    }
    
    public static void RegisterType(Type type, StableComponentId stableId)
    {
        lock (_registrationLock)
        {
            // Avoid duplicates if assembly registered twice or types overlap
            if (StableToDense.TryGetValue(stableId, out var existingLocalId))
            {
                if (StableToType[stableId] == type)
                {
                    // Already registered to the same type, ignore
                    return;
                }
                throw new Exception($"Duplicated stable ids {stableId} for types {StableToType[stableId].Name} and {type.Name}");
            }

            var localId = _nextLocalId++;
            var localCompId = new DenseComponentId(localId);
                
            StableToDense[stableId] = localCompId;
            StableToType[stableId] = type;
                
            DenseToStable[localCompId] = stableId;
            TypeToStable[type] = stableId;
            
            // Update Static Cache (ComponentId<T>) if it exists
            if (TypeToUpdateCache.TryGetValue(type, out var updateAction))
            {
                updateAction(stableId, localCompId);
            }
            
            Console.WriteLine($"[ComponentId] Registered {type.Name} -> Local({localId}) Stable({stableId})");
        }
    }
    
    // Public accessors for advanced scenarios (serializers)
    public static bool TryGetDense(StableComponentId stableId, out DenseComponentId denseId) => StableToDense.TryGetValue(stableId, out denseId);
    public static bool TryGetStable(DenseComponentId denseId, out StableComponentId stableId) => DenseToStable.TryGetValue(denseId, out stableId);
    public static bool TryGetType(DenseComponentId denseId, out Type? type) 
    {
        type = null;
        if (DenseToStable.TryGetValue(denseId, out var stableId))
        {
            return StableToType.TryGetValue(stableId, out type);
        }
        return false;
    }

    /// <summary>
    /// Clears all runtime mapping data (StableToDense/DenseToStable). 
    /// Used during state deserialization. Does NOT clear Type registrations.
    /// </summary>
    public static void ClearMappings()
    {
        lock (_registrationLock)
        {
            StableToDense.Clear();
            DenseToStable.Clear();
            _nextLocalId = 0;
        }
    }

    /// <summary>
    /// Clears ALL data including Type registrations. 
    /// Use ONLY for testing to reset static state.
    /// </summary>
    public static void UnregisterAll()
    {
        lock (_registrationLock)
        {
            ClearMappings();
            StableToType.Clear();
            TypeToStable.Clear();
            // Do NOT clear TypeToUpdateCache as static constructors of generic types run only once
            // and we need to preserve the update delegates.
        }
    }

    /// <summary>
    /// Registers a specific mapping from StableId to DenseId.
    /// Updates all internal lookups and refreshes the ComponentId&lt;T&gt; static cache.
    /// </summary>
    public static void RegisterMapping(StableComponentId stableId, DenseComponentId denseId)
    {
        if (!StableToType.TryGetValue(stableId, out var type)) 
        {
            // Warning: Server has component which is unknown to Client.
            // We cannot map it because we don't have the Type.
            return;
        }
        
        lock (_registrationLock)
        {
            // Update Mappings
            StableToDense[stableId] = denseId;
            DenseToStable[denseId] = stableId;
            TypeToStable[type] = stableId; 
            
            // Update Static Cache (ComponentId<T>)
            if (TypeToUpdateCache.TryGetValue(type, out var updateAction))
            {
                updateAction(stableId, denseId);
            }
            else
            {
                // Fallback? If static ctor hasn't run, the action isn't registered.
                // But if static ctor hasn't run, then `ComponentId<T>.Id` hasn't been accessed yet.
                // When it IS accessed, it will call `FromType<T>`, which uses the updated dictionaries.
                // So we are safe.
            }
            
            if (denseId.Value >= _nextLocalId) _nextLocalId = denseId.Value + 1;
        }
    }

    public int CompareTo(ComponentId other) => _stableValue.CompareTo(other._stableValue);
}
