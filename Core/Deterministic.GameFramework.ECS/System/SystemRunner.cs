using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

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
