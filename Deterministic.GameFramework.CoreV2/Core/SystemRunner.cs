using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Deterministic.GameFramework.CoreV2;

public class SystemRunner
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
}
