using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Deterministic.GameFramework.ECS;

// TODO: Currently it doesnt use counter, so it sometimes may disable when we dont want.
public class SystemRunnerDisposable(SystemRunner runner, IEnumerable<ISystem>? systemsToDisable) : IDisposable
{
    private IEnumerable<ISystem>? _systemsToDisable = systemsToDisable;

    public void Dispose()
    {
        if (_systemsToDisable == null) return;
        
        runner.DisableSystems(_systemsToDisable);
        _systemsToDisable = null;
    }
}

public class SystemRunner
{
    private readonly List<ISystem> _systems = new();

    public SystemRunnerDisposable EnableSystem(ISystem system)
    {
        if (_systems.Contains(system)) return new SystemRunnerDisposable(this, null);
        _systems.Add(system);
        SortSystems();
        return new SystemRunnerDisposable(this, new[] { system });
    }

    public SystemRunnerDisposable EnableSystems(IEnumerable<ISystem> systems)
    {
        var enumerable = systems.ToList();
        var systemsToAdd = enumerable.Where(s => !_systems.Contains(s)).ToList();
        
        _systems.AddRange(systemsToAdd);
        SortSystems();
        
        return new SystemRunnerDisposable(this, systemsToAdd);
    }

    public void DisableSystem(ISystem system)
    {
        _systems.Remove(system);
    }

    public void DisableSystems(IEnumerable<ISystem> systems)
    {
        foreach (var system in systems)
        {
            _systems.Remove(system);
        }
    }

    public bool HasSystem(Type systemType)
    {
        return _systems.Any(s => s.GetType() == systemType);
    }

    private void SortSystems()
    {
        _systems.Sort((a, b) =>
        {
            var orderA = a.GetType().GetCustomAttribute<UpdateOrderAttribute>()?.Order ?? 0;
            var orderB = b.GetType().GetCustomAttribute<UpdateOrderAttribute>()?.Order ?? 0;
            return orderA.CompareTo(orderB);
        });
    }

    public void Update(EntityWorld state)
    {
        // Collect async systems and run their SyncFrom + Step in parallel,
        // while regular systems wait for all async work to finish first.
        var asyncSystems = new List<IAsyncSystem>();
        var asyncTasks = new List<Task>();

        foreach (var system in _systems)
        {
            if (system is IAsyncSystem asyncSystem)
            {
                try
                {
                    asyncSystem.SyncFrom(state);
                    var task = Task.Run(() => asyncSystem.Step());
                    asyncSystems.Add(asyncSystem);
                    asyncTasks.Add(task);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SystemRunner] Error in system {system.GetType().Name} SyncFrom: {ex}");
                }
            }
            else
            {
                // Wait for all async systems before running any regular system
                WaitForAllAsync(asyncSystems, asyncTasks, state);

                try
                {
                    system.Update(state);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SystemRunner] Error in system {system.GetType().Name}: {ex}");
                }
            }
        }

        // Wait for any remaining async systems
        WaitForAllAsync(asyncSystems, asyncTasks, state);
    }

    private static void WaitForAllAsync(List<IAsyncSystem> asyncSystems, List<Task> asyncTasks, EntityWorld state)
    {
        if (asyncTasks.Count == 0) return;

        try
        {
            Task.WaitAll(asyncTasks.ToArray());
        }
        catch (AggregateException ex)
        {
            foreach (var inner in ex.InnerExceptions)
            {
                Console.WriteLine($"[SystemRunner] Error in async system Step: {inner}");
            }
        }

        // SyncTo in deterministic order (same order they were registered)
        for (int i = 0; i < asyncSystems.Count; i++)
        {
            try
            {
                asyncSystems[i].SyncTo(state);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SystemRunner] Error in system {asyncSystems[i].GetType().Name} SyncTo: {ex}");
            }
        }

        asyncSystems.Clear();
        asyncTasks.Clear();
    }
}
