using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Linq;
using System.IO;

namespace Deterministic.GameFramework.CoreV2;

public class GlobalState : IActionDispatcher
{
    internal Array[] _componentArrays = new Array[128];
    internal int[] _componentElementSizes = new int[128];
    internal Type[] _componentTypes = new Type[128];
    internal BitMask128[] _entityMasks = new BitMask128[256]; // Grows with Entity ID
    internal int _nextEntityId = 0;
    
    public int NextEntityId => _nextEntityId;
    public BitMask128[] EntityMasks => _entityMasks;
    
    // Dirty tracking
    internal System.Collections.BitArray _dirtyEntitySet = new System.Collections.BitArray(256);
    internal List<int> _dirtyEntities = new List<int>(64);

    public GameLoop GameLoop { get; internal set; }

    public void Dispatch<TAction>(TAction action, Entity target) where TAction : struct, IAction
    {
        GameLoop.ScheduleOnTick(GameLoop.CurrentTick + 1, action, target);
    }

    public GlobalState()
    {
        var worldEntity = CreateEntity();
        AddComponent(worldEntity, new World());
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
        ref var storage = ref GetComponent<T>(entity);
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
                    Array.Clear(_componentArrays[i], entity.Id, 1);
                }
            }
        }

        // Clear the mask, effectively removing the entity from all queries
        _entityMasks[entity.Id].Clear();
    }

    public ref T GetComponent<T>(Entity entity) where T : struct, IComponent
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
        return ref specificArray[entity.Id];
    }
    
    public T? TryGetComponent<T>(Entity entity) where T : struct, IComponent
    {
        if (!HasComponent<T>(entity)) return null;
        return GetComponent<T>(entity);
    }
    
    public bool HasComponent<T>(Entity entity) where T : struct, IComponent
    {
        if (entity.Id >= _entityMasks.Length) return false;
        
        var typeId = InternalTypeId<T>.Value;
        return _entityMasks[entity.Id].IsSet(typeId);
    }

    public IEnumerable<Entity> Filter<T>() where T : struct, IComponent
    {
        var typeId = InternalTypeId<T>.Value;
        
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

    public delegate void ComponentAction<T1, T2>(ref T1 c1, ref T2 c2);
    public delegate void ComponentActionEntity<T1, T2>(Entity e, ref T1 c1, ref T2 c2);

    public void ForEach<T1, T2>(ComponentAction<T1, T2> action)
        where T1 : struct, IComponent
        where T2 : struct, IComponent
    {
        var mask = new BitMask128();
        var t1Id = InternalTypeId<T1>.Value;
        var t2Id = InternalTypeId<T2>.Value;
        mask.Set(t1Id);
        mask.Set(t2Id);

        EnsureTypedCapacity<T1>(t1Id);
        EnsureTypedCapacity<T2>(t2Id);

        var t1Array = (T1[])_componentArrays[t1Id];
        var t2Array = (T2[])_componentArrays[t2Id];

        for (int i = 0; i < _entityMasks.Length; i++)
        {
            if (_entityMasks[i].HasAll(mask))
            {
                action(ref t1Array[i], ref t2Array[i]);
            }
        }
    }

    public void ForEach<T1, T2>(ComponentActionEntity<T1, T2> action)
        where T1 : struct, IComponent
        where T2 : struct, IComponent
    {
        var mask = new BitMask128();
        var t1Id = InternalTypeId<T1>.Value;
        var t2Id = InternalTypeId<T2>.Value;
        mask.Set(t1Id);
        mask.Set(t2Id);

        EnsureTypedCapacity<T1>(t1Id);
        EnsureTypedCapacity<T2>(t2Id);

        var t1Array = (T1[])_componentArrays[t1Id];
        var t2Array = (T2[])_componentArrays[t2Id];

        for (int i = 0; i < _entityMasks.Length; i++)
        {
            if (_entityMasks[i].HasAll(mask))
            {
                action(new Entity(i), ref t1Array[i], ref t2Array[i]);
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

        if (_componentTypes[typeId] == null)
        {
            if (ComponentTypeRegistry.DenseIdToType.TryGetValue(typeId, out var type))
            {
                _componentTypes[typeId] = type;
                _componentElementSizes[typeId] = Marshal.SizeOf(type);
            }
        }
    }

    internal void EnsureTypedCapacity<T>(int typeId) where T : struct, IComponent
    {
        EnsureTypedCapacityInternal(typeId);

        if (_componentArrays[typeId] == null)
        {
            _componentArrays[typeId] = new T[256];
            _componentElementSizes[typeId] = Marshal.SizeOf<T>();
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

public static class ComponentTypeRegistry
{
    private static int _nextDenseId = 0;
    // Map NetworkId (stable) -> DenseId (runtime index)
    public static readonly Dictionary<Guid, int> NetworkIdToDenseId = new();
    // Map DenseId -> NetworkId
    public static readonly Dictionary<int, Guid> DenseIdToNetworkId = new();
    // Map DenseId -> Type
    public static readonly Dictionary<int, Type> DenseIdToType = new();
    
    // Delegate to resolve Type from NetworkId (e.g. from Generated Registry)
    public static Func<Guid, Type?> TypeResolver { get; set; } = DefaultTypeResolver;

    private static bool _isInitialized = false;
    private static readonly object _initLock = new();

    // Export mappings for handshake (NetworkId -> DenseId)
    public static Dictionary<Guid, int> ExportMappings()
    {
        InitializeIfNeeded();
        lock (NetworkIdToDenseId)
        {
            return new Dictionary<Guid, int>(NetworkIdToDenseId);
        }
    }

    // Import mappings from handshake
    public static void ImportMappings(Dictionary<Guid, int> mappings)
    {
        InitializeIfNeeded();
        lock (NetworkIdToDenseId)
        {
            foreach (var kvp in mappings)
            {
                var networkId = kvp.Key;
                var denseId = kvp.Value;

                if (NetworkIdToDenseId.TryGetValue(networkId, out int existingDenseId))
                {
                    if (existingDenseId != denseId)
                    {
                        // Collision or mismatch!
                        // In a real scenario, we might need to remap or error.
                        // For now, we trust the authoritative source (usually server) 
                        // and might need to adjust our local denseId if we already allocated one.
                        // But ComponentTypeRegistry is usually static/global. 
                        // Remapping DenseIds at runtime is hard if arrays are already allocated.
                        // Ideally, ImportMappings happens BEFORE any components are created.
                        Console.WriteLine($"[ComponentTypeRegistry] Warning: NetworkId {networkId} already mapped to {existingDenseId}, but import requests {denseId}. Keeping existing.");
                    }
                    continue;
                }

                // If denseId is already taken by another NetworkId?
                if (DenseIdToNetworkId.ContainsKey(denseId))
                {
                    // Shift local denseId? Or error?
                    Console.WriteLine($"[ComponentTypeRegistry] Warning: DenseId {denseId} already used. Cannot import mapping for {networkId}.");
                    continue;
                }

                NetworkIdToDenseId[networkId] = denseId;
                DenseIdToNetworkId[denseId] = networkId;
                
                // We don't necessarily know the Type here if it hasn't been registered yet.
                // But usually we register types locally first, which assigns random denseIds.
                // This Import should ideally set the denseIds to match the server.
                // Implementation detail: If we call GetOrRegister later, it checks NetworkIdToDenseId first.
                // So if we pre-populate this, it works.
                
                // Update _nextDenseId to avoid collisions
                if (denseId >= _nextDenseId)
                {
                    _nextDenseId = denseId + 1;
                }
            }
        }
    }

    private static void InitializeIfNeeded()
    {
        if (_isInitialized) return;
        lock (_initLock)
        {
            if (_isInitialized) return;
            _isInitialized = true; // Set early to prevent recursion during RegisterAll calls

            var domainId = AppDomain.CurrentDomain.Id;
            var threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            
            // Use the same logic as ServiceLocator to find assemblies
            var assemblies = new HashSet<Assembly>(AppDomain.CurrentDomain.GetAssemblies());
            
            // Try to find the entry assembly and its references
            var entryAssembly = Assembly.GetEntryAssembly();
            if (entryAssembly != null)
            {
                LoadReferencedAssemblies(entryAssembly, assemblies);
            }

            // Also scan directory for DLLs
            try 
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var dlls = Directory.GetFiles(baseDir, "*.dll", SearchOption.TopDirectoryOnly);
                
                foreach (var dllPath in dlls)
                {
                    try 
                    {
                        var fileName = Path.GetFileNameWithoutExtension(dllPath);
                        if (IsIgnoredName(fileName)) continue;
                        if (assemblies.Any(a => a.GetName().Name == fileName)) continue;

                        var loadedAssembly = Assembly.LoadFrom(dllPath);
                        if (!IsIgnoredAssembly(loadedAssembly))
                        {
                            if (assemblies.Add(loadedAssembly))
                            {
                                LoadReferencedAssemblies(loadedAssembly, assemblies);
                            }
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ComponentTypeRegistry] Warning: Failed to scan directory assemblies: {ex.Message}");
            }

            var processedAssemblies = new HashSet<string>();

            // Scan all found assemblies for the generated RegisterAll method
            foreach (var assembly in assemblies)
            {
                if (IsIgnoredAssembly(assembly)) continue;
                
                var assemblyName = assembly.GetName().Name;
                if (assemblyName != null && processedAssemblies.Contains(assemblyName)) continue;
                if (assemblyName != null) processedAssemblies.Add(assemblyName);

                // Look for the generated registry
                var registryType = assembly.GetType("Deterministic.GameFramework.Generated.NetworkIdRegistry");
                if (registryType != null)
                {
                    var registerMethod = registryType.GetMethod("RegisterAll", BindingFlags.Public | BindingFlags.Static);
                    if (registerMethod != null)
                    {
                        try
                        {
                            registerMethod.Invoke(null, null);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[ComponentTypeRegistry] Failed to invoke RegisterAll in {assemblyName}: {ex.Message}");
                        }
                    }
                }
            }
        }
    }

    private static void LoadReferencedAssemblies(Assembly assembly, HashSet<Assembly> loadedAssemblies)
    {
        try
        {
            foreach (var refName in assembly.GetReferencedAssemblies())
            {
                if (IsIgnoredName(refName.Name)) continue;
                if (loadedAssemblies.Any(a => a.GetName().Name == refName.Name)) continue;

                try
                {
                    var loaded = Assembly.Load(refName);
                    if (loadedAssemblies.Add(loaded))
                    {
                        LoadReferencedAssemblies(loaded, loadedAssemblies);
                    }
                }
                catch { }
            }
        }
        catch { }
    }

    private static bool IsIgnoredName(string? name)
    {
        if (string.IsNullOrEmpty(name)) return true;
        return name.StartsWith("System") || name.StartsWith("Microsoft") || name.StartsWith("mscorlib") || name.StartsWith("netstandard");
    }

    private static bool IsIgnoredAssembly(Assembly assembly)
    {
        try
        {
            if (assembly.IsDynamic) return true;
            return IsIgnoredName(assembly.GetName().Name);
        }
        catch
        {
            return true;
        }
    }

    private static Type? DefaultTypeResolver(Guid networkId)
    {
        InitializeIfNeeded();
        
        lock (NetworkIdToDenseId)
        {
            if (NetworkIdToDenseId.TryGetValue(networkId, out var denseId))
            {
                return DenseIdToType[denseId];
            }
        }
        return null;
    }

    public static int GetOrRegister(Guid networkId, Type type)
    {
        InitializeIfNeeded();
        lock (NetworkIdToDenseId)
        {
            if (!NetworkIdToDenseId.TryGetValue(networkId, out var denseId))
            {
                denseId = _nextDenseId++;
                NetworkIdToDenseId[networkId] = denseId;
                DenseIdToNetworkId[denseId] = networkId;
                DenseIdToType[denseId] = type;
                Console.WriteLine($"[ComponentTypeRegistry] Mapping NetworkId {networkId} -> DenseId {denseId} ({type.FullName})");
            }
            return denseId;
        }
    }
    
    public static int GetOrRegister(Guid networkId)
    {
        InitializeIfNeeded();

        // 1. Quick check under lock
        lock (NetworkIdToDenseId)
        {
            if (NetworkIdToDenseId.TryGetValue(networkId, out var denseId))
            {
                return denseId;
            }
        }

        Console.WriteLine($"[ComponentTypeRegistry] Requesting unknown NetworkId {networkId}. Attempting resolution...");

        // 2. Resolve type outside of lock to avoid deadlock
        if (TypeResolver == null)
        {
            throw new Exception($"Unknown NetworkId {networkId} and no TypeResolver configured.");
        }

        var type = TypeResolver(networkId);
        if (type == null)
        {
            lock (NetworkIdToDenseId)
            {
                Console.WriteLine($"[ComponentTypeRegistry] ERROR: Could not resolve NetworkId {networkId}. Current Mappings: {string.Join(", ", NetworkIdToDenseId.Select(kv => $"{kv.Key}->{kv.Value} ({DenseIdToType[kv.Value].Name})"))}");
            }
            throw new Exception($"TypeResolver returned null for NetworkId {networkId}. Ensure the component has [NetworkId(\"{networkId}\")] and its assembly is loaded.");
        }

        // 3. Register under lock (handling race)
        return GetOrRegister(networkId, type);
    }
    
    public static bool TryGetDenseId(Guid networkId, out int denseId)
    {
        lock (NetworkIdToDenseId)
        {
            return NetworkIdToDenseId.TryGetValue(networkId, out denseId);
        }
    }

    public static bool TryGetNetworkId(int denseId, out Guid networkId)
    {
        lock (NetworkIdToDenseId)
        {
            return DenseIdToNetworkId.TryGetValue(denseId, out networkId);
        }
    }
}

public static class InternalTypeId<T> where T : struct, IComponent
{
    public static readonly Guid NetworkId = GetNetworkId();
    public static readonly int Value = ComponentTypeRegistry.GetOrRegister(NetworkId, typeof(T));

    private static Guid GetNetworkId()
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
