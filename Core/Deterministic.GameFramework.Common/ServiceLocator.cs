
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Deterministic.GameFramework.DAR;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Utils.Logging;

namespace Deterministic.GameFramework.Common;

public class ServiceRegistration
{
    public SystemRunnerDisposable? Systems { get; set; }
    public ActionRunnerDisposable? Actions { get; set; }
    public IDisposable? Reactions { get; set; }

    public List<IActionService> RegisteredActionServices { get; set; } = new();
}

public static class ServiceLocator
{
    private static readonly HashSet<Assembly> _registeredAssemblies = new();
    
    private static readonly Dictionary<Type, object> _singletons = new();
    private static readonly object _lock = new();

    public static ServiceRegistration Register(GameSimulation simulation, IEnumerable<Assembly> assemblies)
    {
        var systems = new List<ISystem>();
        var actions = new List<IActionService>();

        foreach (var assembly in assemblies)
        {
            RegisterAssembly(assembly);

            foreach (var type in assembly.GetTypes())
            {
                if (type.IsAbstract || type.IsInterface) continue;

                if (typeof(ISystem).IsAssignableFrom(type))
                {
                    if (type.GetCustomAttributes(false).Any(a => a.GetType().Name == "ManualSystemAttribute"))
                        continue;
                    systems.Add((ISystem)GetOrCreate(type));
                }
                else if (typeof(IActionService).IsAssignableFrom(type))
                {
                    actions.Add((IActionService)GetOrCreate(type));
                }
            }
        }

        simulation.Dispatcher.RegisterServices(actions);

        var registration = new ServiceRegistration
        {
            Systems = simulation.SystemRunner.EnableSystems(systems),
            Actions = simulation.Dispatcher.EnableActions(actions),
            RegisteredActionServices = actions
        };

        return registration;
    }

    public static void Unregister(GameSimulation simulation, ServiceRegistration registration)
    {
        registration.Systems?.Dispose();
        registration.Actions?.Dispose();
        registration.Reactions?.Dispose();

        simulation.Dispatcher.UnregisterServices(registration.RegisteredActionServices);
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
                ILogger.LogError($"[ServiceLocator] Failed to instantiate {type.Name}: {ex.Message}");
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
            
            ILogger.Log($"[ServiceLocator] Registered assembly: {assembly.GetName().Name}");
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
                        // Skip systems marked [ManualSystem]
                        if (typeof(ISystem).IsAssignableFrom(interfaceType) &&
                            type.GetCustomAttributes(false).Any(a => a.GetType().Name == "ManualSystemAttribute"))
                            continue;
                        if (!_singletons.TryGetValue(type, out var instance))
                        {
                            try
                            {
                                instance = Activator.CreateInstance(type)!;
                                _singletons[type] = instance;
                            }
                            catch (Exception ex)
                            {
                                ILogger.LogError($"[ServiceLocator] Failed to instantiate {type.Name}: {ex.Message}");
                                continue;
                            }
                        }
                        implementations.Add((T)instance);
                    }
                }
            }
        }

        // Sort for determinism (Critical for System execution order)
        implementations.Sort((a, b) =>
        {
            string? nameA = a?.GetType().FullName;
            string? nameB = b?.GetType().FullName;
            return string.Compare(nameA, nameB, StringComparison.Ordinal);
        });

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

