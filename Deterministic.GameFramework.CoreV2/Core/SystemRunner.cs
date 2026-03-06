using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Deterministic.GameFramework.CoreV2;

public class SystemRunner : IDisposable
{
    private readonly List<ISystem> _systems = new();

    public void RegisterSystem(ISystem system)
    {
        _systems.Add(system);
        SortSystems();
    }

    public void RegisterSystems(IEnumerable<ISystem> systems)
    {
        _systems.AddRange(systems);
        SortSystems();
    }

    public void RemoveSystem(ISystem system)
    {
        _systems.Remove(system);
    }

    public void RemoveSystems(IEnumerable<ISystem> systems)
    {
        foreach (var system in systems)
        {
            _systems.Remove(system);
        }
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

    public void Update(GlobalState state)
    {
        foreach (var system in _systems)
        {
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

    public void Dispose()
    {
        foreach (var system in _systems)
        {
            if (system is IDisposable disposable)
            {
                try
                {
                    disposable.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SystemRunner] Error disposing system {system.GetType().Name}: {ex}");
                }
            }
        }
        _systems.Clear();
    }
}
