
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Deterministic.GameFramework.CoreV2;

public static class ServiceLocator
{
    public static readonly Dictionary<Type, Guid> TypeToId = new Dictionary<Type, Guid>();
    public static readonly Dictionary<Guid, Type> IdToType = new Dictionary<Guid, Type>();

    /// <summary>
    /// Scans all loaded assemblies for types with [NetworkId] and registers services to the Dispatcher.
    /// Also proactively loads referenced assemblies and scans the base directory to ensure game logic is found.
    /// </summary>
    public static void Initialize(Dispatcher dispatcher)
    {
        var assemblies = new HashSet<Assembly>(AppDomain.CurrentDomain.GetAssemblies().Where(a => !IsIgnoredAssembly(a)));
        
        // 1. Eager load referenced assemblies
        var entryAssembly = Assembly.GetEntryAssembly();
        if (entryAssembly != null && !IsIgnoredAssembly(entryAssembly))
        {
            assemblies.Add(entryAssembly);
            LoadReferencedAssemblies(entryAssembly, assemblies);
        }

        // 2. Scan directory for DLLs (Plugins / Shared libraries not yet loaded)
        try 
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var dlls = Directory.GetFiles(baseDir, "*.dll", SearchOption.TopDirectoryOnly);
            
            Console.WriteLine($"[ServiceLocator] Scanning base directory: {baseDir}");
            foreach (var dllPath in dlls)
            {
                try 
                {
                    var fileName = Path.GetFileNameWithoutExtension(dllPath);
                    if (IsIgnoredName(fileName)) 
                    {
                        // Console.WriteLine($"[ServiceLocator] Ignored by name: {fileName}");
                        continue;
                    }

                    // Avoid re-loading if already in memory (by simple name check)
                    if (assemblies.Any(a => a.GetName().Name == fileName)) 
                    {
                        Console.WriteLine($"[ServiceLocator] Already loaded: {fileName}");
                        continue;
                    }

                    Console.WriteLine($"[ServiceLocator] Loading external assembly: {fileName}");
                    var loadedAssembly = Assembly.LoadFrom(dllPath);
                    
                    if (!IsIgnoredAssembly(loadedAssembly))
                    {
                        if (assemblies.Add(loadedAssembly))
                        {
                            Console.WriteLine($"[ServiceLocator] Successfully added: {loadedAssembly.FullName}");
                            LoadReferencedAssemblies(loadedAssembly, assemblies);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[ServiceLocator] Ignored after load: {fileName}");
                    }
                }
                catch (Exception ex)
                { 
                    Console.WriteLine($"[ServiceLocator] Failed to load {dllPath}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ServiceLocator] Warning: Failed to scan directory assemblies: {ex.Message}");
        }

        Console.WriteLine($"[ServiceLocator] Final assembly count: {assemblies.Count}");
        Initialize(dispatcher, assemblies);
    }

    private static bool IsIgnoredName(string name)
    {
        return name != null && (name.StartsWith("System") || name.StartsWith("Microsoft") || name.StartsWith("mscorlib") || name.StartsWith("netstandard"));
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

    private static void LoadReferencedAssemblies(Assembly assembly, HashSet<Assembly> loadedAssemblies)
    {
        AssemblyName[] references;
        try
        {
            references = assembly.GetReferencedAssemblies();
        }
        catch
        {
            return; // Can't get references
        }

        foreach (var refName in references)
        {
            // Optimization: Skip system/microsoft assemblies
            if (refName.Name != null && (refName.Name.StartsWith("System") || refName.Name.StartsWith("Microsoft") || refName.Name.StartsWith("mscorlib") || refName.Name.StartsWith("netstandard")))
                continue;

            try
            {
                // Check if already loaded by name
                if (loadedAssemblies.Any(a => a.GetName().Name == refName.Name)) 
                    continue;

                var loadedAssembly = Assembly.Load(refName);
                
                if (!IsIgnoredAssembly(loadedAssembly))
                {
                    if (loadedAssemblies.Add(loadedAssembly))
                    {
                        // Recurse into user/game assemblies
                        LoadReferencedAssemblies(loadedAssembly, loadedAssemblies);
                    }
                }
            }
            catch
            {
                // Ignore load errors (optional dependencies, etc.)
            }
        }
    }

    /// <summary>
    /// Scans specified assemblies for types with [NetworkId] and registers services to the Dispatcher.
    /// </summary>
    public static void Initialize(Dispatcher dispatcher, IEnumerable<Assembly> assemblies)
    {
        var types = assemblies.SelectMany(a => a.GetTypes()).ToArray();

        // 1. Build NetworkId Map
        TypeToId.Clear();
        IdToType.Clear();

        foreach (var type in types)
        {
            var attr = type.GetCustomAttribute<NetworkIdAttribute>();
            if (attr != null)
            {
                if (TypeToId.ContainsKey(type))
                {
                    Console.WriteLine($"[ServiceLocator] Warning: Duplicate NetworkId for type {type.Name}");
                    continue;
                }

                if (IdToType.ContainsKey(attr.Id))
                {
                    Console.WriteLine($"[ServiceLocator] Warning: Duplicate NetworkId {attr.Id} for type {type.Name} (Collision with {IdToType[attr.Id].Name})");
                    continue;
                }

                TypeToId[type] = attr.Id;
                IdToType[attr.Id] = type;
                // Console.WriteLine($"[ServiceLocator] Mapped {type.Name} -> {attr.Id}");
            }
        }

        // 2. Register Services
        RegisterServices(dispatcher, types);
    }

    public static void Initialize(GameLoop loop)
    {
        var assemblies = new HashSet<Assembly>(AppDomain.CurrentDomain.GetAssemblies().Where(a => !IsIgnoredAssembly(a)));
        
        // 1. Eager load referenced assemblies
        var entryAssembly = Assembly.GetEntryAssembly();
        if (entryAssembly != null && !IsIgnoredAssembly(entryAssembly))
        {
            assemblies.Add(entryAssembly);
            LoadReferencedAssemblies(entryAssembly, assemblies);
        }

        Initialize(loop, assemblies);
    }

    public static void Initialize(GameLoop loop, IEnumerable<Assembly> assemblies)
    {
        var types = assemblies.SelectMany(a => a.GetTypes()).ToArray();
        RegisterSystems(loop, types);
    }

    private static void RegisterSystems(GameLoop loop, IEnumerable<Type> types)
    {
        var systems = new List<ISystem>();
        foreach (var type in types)
        {
            if (typeof(ISystem).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
            {
                try
                {
                    var system = (ISystem)Activator.CreateInstance(type)!;
                    systems.Add(system);
                    Console.WriteLine($"[ServiceLocator] Found System: {type.Name}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ServiceLocator] Failed to instantiate system {type.Name}: {ex.Message}");
                }
            }
        }
        loop.RegisterSystems(systems);
    }

    private static void RegisterServices(Dispatcher dispatcher, IEnumerable<Type> types)
    {
        // Find all ReactionServices
        var reactionMap = new Dictionary<(Type ActionType, Type TargetType), List<object>>();
        var allReactions = new List<(Type type, Type actionType, Type targetType, object instance)>();
        
        foreach (var type in types)
        {
            if (type.IsAbstract || type.IsInterface) continue;
            
            var baseType = GetGenericBaseType(type, typeof(ReactionService<,>));
            if (baseType != null)
            {
                var genericArgs = baseType.GetGenericArguments();
                var actionType = genericArgs[0];
                var targetType = genericArgs[1];
                var key = (actionType, targetType);
                
                if (!reactionMap.ContainsKey(key))
                {
                    reactionMap[key] = new List<object>();
                }
                
                try
                {
                    var reaction = Activator.CreateInstance(type);
                    if (reaction != null)
                    {
                        reactionMap[key].Add(reaction);
                        allReactions.Add((type, actionType, targetType, reaction));
                    }
                }
                catch (Exception ex)
                {
                     Console.WriteLine($"[ServiceLocator] Failed to instantiate reaction {type.Name}: {ex.Message}");
                }
            }
        }

        // Track which reactions are consumed by ActionServices
        var consumedReactions = new HashSet<(Type ActionType, Type TargetType)>();

        // Find and Register all ActionServices
        foreach (var type in types)
        {
            if (type.IsAbstract || type.IsInterface) continue;

            var baseType = GetGenericBaseType(type, typeof(ActionService<,>));
            if (baseType != null)
            {
                var genericArgs = baseType.GetGenericArguments();
                var actionType = genericArgs[0];
                var targetType = genericArgs[1];
                
                try
                {
                    var service = Activator.CreateInstance(type);
                    
                    // Find matching reactions
                    Array reactions;
                    var reactionType = typeof(ReactionService<,>).MakeGenericType(actionType, targetType);

                    if (reactionMap.TryGetValue((actionType, targetType), out var reactionList))
                    {
                        reactions = Array.CreateInstance(reactionType, reactionList.Count);
                        for (int i = 0; i < reactionList.Count; i++)
                        {
                            reactions.SetValue(reactionList[i], i);
                        }
                        consumedReactions.Add((actionType, targetType));
                    }
                    else
                    {
                        reactions = Array.CreateInstance(reactionType, 0);
                    }

                    // Call dispatcher.RegisterAction<TAction, TTarget>(service, reactions)
                    var registerMethod = typeof(Dispatcher).GetMethod(nameof(Dispatcher.RegisterAction))?
                        .MakeGenericMethod(actionType, targetType);
                    
                    registerMethod?.Invoke(dispatcher, new[] { service, reactions });
                    
                    Console.WriteLine($"[ServiceLocator] Registered ActionService: {type.Name} for {actionType.Name} -> {targetType.Name}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ServiceLocator] Failed to register {type.Name}: {ex.Message}");
                }
            }
        }

        // Register standalone reactions (not consumed by any ActionService)
        foreach (var (type, actionType, targetType, instance) in allReactions)
        {
            if (!consumedReactions.Contains((actionType, targetType)))
            {
                try
                {
                    // Call dispatcher.RegisterReaction<TAction, TTarget>(reaction)
                    var registerReactionMethod = typeof(Dispatcher).GetMethod(nameof(Dispatcher.RegisterReaction))?
                        .MakeGenericMethod(actionType, targetType);
                    
                    registerReactionMethod?.Invoke(dispatcher, new[] { instance });
                    
                    Console.WriteLine($"[ServiceLocator] Registered standalone ReactionService: {type.Name} for {actionType.Name} -> {targetType.Name}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ServiceLocator] Failed to register standalone reaction {type.Name}: {ex.Message}");
                }
            }
        }
    }

    private static Type? GetGenericBaseType(Type type, Type genericOpenType)
    {
        while (type != null && type != typeof(object))
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == genericOpenType)
            {
                return type;
            }
            type = type.BaseType!;
        }
        return null;
    }
}

