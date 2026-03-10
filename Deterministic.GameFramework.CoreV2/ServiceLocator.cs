
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Deterministic.GameFramework.CoreV2;

public class ServiceRegistration
{
    public SystemRunnerDisposable? Systems { get; set; }
    public ActionRunnerDisposable? Actions { get; set; }
    public ReactionRunnerDisposable? Reactions { get; set; }
    
    // Store raw lists for deep unregistration
    public List<IActionService> RegisteredActionServices { get; set; } = new();
    public List<IReactionService> RegisteredReactionServices { get; set; } = new();
}

public static class ServiceLocator
{
    private static readonly HashSet<Assembly> _registeredAssemblies = new();
    
    private static readonly Dictionary<Type, object> _singletons = new();
    private static readonly object _lock = new();

    // Cache for Action/Reaction types to avoid scanning repeatedly
    // private static readonly List<Type> _actionServiceTypes = new();
    // private static readonly List<Type> _reactionServiceTypes = new();

    public static ServiceRegistration Register(GameLoop loop, IEnumerable<Assembly> assemblies)
    {
        var systems = new List<ISystem>();
        var actions = new List<IActionService>();
        var reactions = new List<IReactionService>();

        foreach (var assembly in assemblies)
        {
            RegisterAssembly(assembly);

            foreach (var type in assembly.GetTypes())
            {
                if (type.IsAbstract || type.IsInterface) continue;

                if (typeof(ISystem).IsAssignableFrom(type))
                {
                    systems.Add((ISystem)GetOrCreate(type));
                }
                else if (typeof(IActionService).IsAssignableFrom(type))
                {
                    actions.Add((IActionService)GetOrCreate(type));
                }
                else if (typeof(IReactionService).IsAssignableFrom(type))
                {
                    reactions.Add((IReactionService)GetOrCreate(type));
                }
            }
        }

        // Register Services with Dispatcher first (ensure IDs are assigned)
        loop.Dispatcher.RegisterServices(actions, reactions);

        var registration = new ServiceRegistration
        {
            Systems = loop.SystemRunner.EnableSystems(systems),
            Actions = loop.Dispatcher.EnableActions(actions),
            Reactions = loop.Dispatcher.EnableReactions(reactions),
            RegisteredActionServices = actions,
            RegisteredReactionServices = reactions
        };

        return registration;
    }

    public static void Unregister(GameLoop loop, ServiceRegistration registration)
    {
        registration.Systems?.Dispose();
        registration.Actions?.Dispose();
        registration.Reactions?.Dispose();
        
        loop.Dispatcher.UnregisterServices(registration.RegisteredActionServices, registration.RegisteredReactionServices);
    }

    /// <summary>
    /// Resets the ServiceLocator state. USE ONLY IN TESTS.
    /// </summary>
    public static void Reset()
    {
        lock (_lock)
        {
            _registeredAssemblies.Clear();
            _singletons.Clear();
            // ComponentId state might also need resetting if it persists static data
            ComponentId.ClearMappings();
        }
    }

    private static object GetOrCreate(Type type)
    {
        lock (_lock)
        {
            if (_singletons.TryGetValue(type, out var instance))
            {
                return instance;
            }

            try
            {
                instance = Activator.CreateInstance(type)!;
                _singletons[type] = instance;
                return instance;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ServiceLocator] Failed to instantiate {type.Name}: {ex.Message}");
                throw;
            }
        }
    }

    /// <summary>
    /// Registers an assembly for component and service discovery.
    /// This should be called during application startup (e.g. in GameFactory).
    /// </summary>
    public static void RegisterAssembly(Assembly assembly)
    {
        lock (_lock)
        {
            _registeredAssemblies.Add(assembly);
            
            // 1. Register Components (Data) via ComponentId
            // Must be done inside lock to prevent race condition where other threads see assembly as registered
            // but components are not yet mapped.
            // Always call this, as ComponentId.RegisterAssembly handles duplicates safely, 
            // and we need to recover if ComponentId mappings were cleared (e.g. in tests).
            ComponentId.RegisterAssembly(assembly);
            
            Console.WriteLine($"[ServiceLocator] Registered assembly: {assembly.GetName().Name}");
        }
    }

    public static T Get<T>()
    {
        var type = typeof(T);
        lock (_lock)
        {
            if (_singletons.TryGetValue(type, out var instance))
            {
                return (T)instance;
            }

            // Find implementation
            var implementation = FindImplementation<T>();
            if (implementation == null)
            {
                throw new Exception($"No implementation found for {type.Name} in registered assemblies.");
            }

            var newInstance = Activator.CreateInstance(implementation)!;
            _singletons[type] = newInstance;
            
            // Also cache by implementation type
            _singletons[implementation] = newInstance;
            
            return (T)newInstance;
        }
    }

    public static IEnumerable<T> GetAll<T>()
    {
        var interfaceType = typeof(T);
        var implementations = new List<T>();

        lock (_lock)
        {
            foreach (var assembly in _registeredAssemblies)
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (interfaceType.IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                    {
                        if (!_singletons.TryGetValue(type, out var instance))
                        {
                            try
                            {
                                instance = Activator.CreateInstance(type)!;
                                _singletons[type] = instance;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[ServiceLocator] Failed to instantiate {type.Name}: {ex.Message}");
                                continue;
                            }
                        }
                        implementations.Add((T)instance);
                    }
                }
            }
        }
        return implementations;
    }

    public static IEnumerable<Type> GetAllTypes()
    {
        return _registeredAssemblies.SelectMany(a => a.GetTypes());
    }

    private static Type? FindImplementation<T>()
    {
        var interfaceType = typeof(T);
        foreach (var assembly in _registeredAssemblies)
        {
            foreach (var type in assembly.GetTypes())
            {
                if (interfaceType.IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                {
                    return type;
                }
            }
        }
        return null;
    }
}

