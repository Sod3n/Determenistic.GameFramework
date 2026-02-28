
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Deterministic.GameFramework.CoreV2;

public static class ServiceLocator
{
    public static readonly Dictionary<Type, int> TypeToId = new Dictionary<Type, int>();
    public static readonly Dictionary<int, Type> IdToType = new Dictionary<int, Type>();

    /// <summary>
    /// Scans all loaded assemblies for types with [NetworkId] and registers services to the Dispatcher.
    /// </summary>
    public static void Initialize(Dispatcher dispatcher)
    {
        Initialize(dispatcher, AppDomain.CurrentDomain.GetAssemblies());
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

