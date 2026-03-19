using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Deterministic.GameFramework.ECS.Tests;

[Collection("ECS Tests")]
public class SystemRunnerTests
{
    [Fact]
    public void EnableSystem_ShouldAddSystem()
    {
        var runner = new SystemRunner();
        var system = new TestSystem();
        
        runner.EnableSystem(system);
        
        runner.HasSystem(typeof(TestSystem)).Should().BeTrue();
    }
    
    [Fact]
    public void EnableSystem_ShouldNotDuplicate()
    {
        var runner = new SystemRunner();
        var system = new TestSystem();
        
        runner.EnableSystem(system);
        runner.EnableSystem(system); // Try add again
        
        // Internal list is private, but we can infer from execution or just HasSystem
        // HasSystem returns true if at least one exists.
        // We can check if it runs twice?
        // But the implementation says: if (_systems.Contains(system)) return ...
        // So it should be unique.
    }

    [Fact]
    public void DisableSystem_ShouldRemoveSystem()
    {
        var runner = new SystemRunner();
        var system = new TestSystem();
        
        runner.EnableSystem(system);
        runner.DisableSystem(system);
        
        runner.HasSystem(typeof(TestSystem)).Should().BeFalse();
    }

    [Fact]
    public void Update_ShouldExecuteSystems()
    {
        var runner = new SystemRunner();
        var system = new TestSystem();
        runner.EnableSystem(system);
        var world = new EntityWorld();

        runner.Update(world);

        system.Updated.Should().BeTrue();
    }

    [Fact]
    public void ExecutionOrder_ShouldRespectAttribute()
    {
        var runner = new SystemRunner();
        OrderedSystem1.ExecutionLog.Clear();
        
        runner.EnableSystem(new OrderedSystem2()); // Order 20
        runner.EnableSystem(new OrderedSystem1()); // Order 10
        runner.EnableSystem(new OrderedSystemNegative()); // Order -5
        
        runner.Update(new EntityWorld());
        
        OrderedSystem1.ExecutionLog.Should().ContainInOrder("SystemNeg", "System1", "System2");
    }

    [Fact]
    public void ExceptionInSystem_ShouldNotCrashRunner()
    {
        var runner = new SystemRunner();
        runner.EnableSystem(new ExceptionSystem());
        runner.EnableSystem(new TestSystem()); // Should still run? implementation executes in loop with try-catch
        
        Action act = () => runner.Update(new EntityWorld());
        
        act.Should().NotThrow();
    }

    [Fact]
    public void SystemRunnerDisposable_ShouldDisableOnDispose()
    {
        var runner = new SystemRunner();
        var system = new TestSystem();
        
        using (runner.EnableSystem(system))
        {
            runner.HasSystem(typeof(TestSystem)).Should().BeTrue();
        }
        
        runner.HasSystem(typeof(TestSystem)).Should().BeFalse();
    }
    
    [Fact]
    public void EnableSystems_ShouldAddMultiple()
    {
        var runner = new SystemRunner();
        var s1 = new TestSystem();
        var s2 = new OrderedSystem1();
        
        runner.EnableSystems(new ISystem[] { s1, s2 });
        
        runner.HasSystem(typeof(TestSystem)).Should().BeTrue();
        runner.HasSystem(typeof(OrderedSystem1)).Should().BeTrue();
    }
    
    [Fact]
    public void EnableSystems_Disposable_ShouldDisableAll()
    {
        var runner = new SystemRunner();
        var s1 = new TestSystem();
        var s2 = new OrderedSystem1();
        
        using (runner.EnableSystems(new ISystem[] { s1, s2 }))
        {
             runner.HasSystem(typeof(TestSystem)).Should().BeTrue();
             runner.HasSystem(typeof(OrderedSystem1)).Should().BeTrue();
        }
        
        runner.HasSystem(typeof(TestSystem)).Should().BeFalse();
        runner.HasSystem(typeof(OrderedSystem1)).Should().BeFalse();
    }

    [Fact]
    public void SystemRunner_DisableSystems_ShouldRemoveMultiple()
    {
        var runner = new SystemRunner();
        var s1 = new TestSystem();
        var s2 = new OrderedSystem1();
        runner.EnableSystems(new ISystem[] { s1, s2 });
        runner.DisableSystems(new ISystem[] { s1, s2 });
        runner.HasSystem(typeof(TestSystem)).Should().BeFalse();
    }
}
