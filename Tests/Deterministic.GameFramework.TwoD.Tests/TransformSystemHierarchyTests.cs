using System.Collections.Generic;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.TwoD;
using Deterministic.GameFramework.Types;
using Deterministic.GameFramework.Common;
using FluentAssertions;
using Xunit;

namespace Deterministic.GameFramework.TwoD.Tests;

public class TransformSystemHierarchyTests
{
    private (EntityWorld state, TransformSystem system) CreateWorld()
    {
        ServiceLocator.Reset();
        ServiceLocator.RegisterAssembly(typeof(EntityWorld).Assembly);
        ServiceLocator.RegisterAssembly(typeof(Transform2D).Assembly);

        var state = new EntityWorld();
        state.RegisterComponent<Transform2D>();
        
        var system = new TransformSystem();
        
        return (state, system);
    }

    [Fact]
    public void Hierarchy_BuildsCorrectly_ForValidParent()
    {
        var (state, system) = CreateWorld();

        var parent = state.CreateEntity();
        state.AddComponent(parent, new Transform2D(Vector2.Zero, 0, Vector2.One));

        var child = state.CreateEntity();
        var tChild = new Transform2D(Vector2.Zero, 0, Vector2.One);
        tChild.Parent = parent;
        state.AddComponent(child, tChild);

        // Run Update
        system.Update(state);
        
        // We can't inspect the private 'hierarchy' dictionary directly, 
        // but we can verify the effects.
        // If hierarchy is built, the child should be updated relative to parent.
        // Let's move parent and see if child moves in the same frame (DFS update).
        
        ref var tParent = ref state.GetComponent<Transform2D>(parent);
        tParent.Position = new Vector2(100, 100);
        // We need to force a global update or just rely on the system to update globals from locals.
        // TransformSystem updates Global from Local if Local changed (which it didn't really here, we just set it).
        // Actually, if we set Position, GlobalPosition isn't automatically updated until System runs.
        
        system.Update(state);
        
        var cChild = state.GetComponent<Transform2D>(child);
        cChild.GlobalPosition.Should().Be(new Vector2(100, 100));
    }

    [Fact]
    public void ParentDestruction_PropagatesTo_DestroyOnUnparentChild()
    {
        var (state, system) = CreateWorld();

        // Setup: Parent (Orphan that dies) -> Child (DestroyOnUnparent=true)
        
        // 1. Create a "Fake" Root Parent that doesn't exist, so 'Parent' becomes an Orphan marked for death.
        var parent = state.CreateEntity();
        var tParent = new Transform2D(Vector2.Zero, 0, Vector2.One);
        tParent.Parent = new Entity(9999); // Invalid
        tParent.DestroyOnUnparent = true;
        state.AddComponent(parent, tParent);

        // 2. Create Child
        var child = state.CreateEntity();
        var tChild = new Transform2D(Vector2.Zero, 0, Vector2.One);
        tChild.Parent = parent;
        tChild.DestroyOnUnparent = true;
        state.AddComponent(child, tChild);

        // Update
        system.Update(state);

        // Parent should be destroyed
        state.HasComponent<Transform2D>(parent).Should().BeFalse();
        
        // Child should be destroyed (propagated)
        state.HasComponent<Transform2D>(child).Should().BeFalse();
    }

    [Fact]
    public void ParentDestruction_Reparents_SurvivorChild_ToRoot()
    {
        var (state, system) = CreateWorld();

        // Setup: Parent (Orphan that dies) -> Child (DestroyOnUnparent=false)
        
        var parent = state.CreateEntity();
        var tParent = new Transform2D(Vector2.Zero, 0, Vector2.One);
        tParent.Parent = new Entity(9999); // Invalid
        tParent.DestroyOnUnparent = true;
        state.AddComponent(parent, tParent);

        var child = state.CreateEntity();
        var tChild = new Transform2D(new Vector2(10, 10), 0, Vector2.One);
        tChild.Parent = parent;
        tChild.DestroyOnUnparent = false; // Survives
        state.AddComponent(child, tChild);

        // Update
        system.Update(state);

        // Parent destroyed
        state.HasComponent<Transform2D>(parent).Should().BeFalse();

        // Child survives
        state.HasComponent<Transform2D>(child).Should().BeTrue();
        
        // Child should become root (Parent = Null)
        var cTransform = state.GetComponent<Transform2D>(child);
        cTransform.Parent.Should().Be(Entity.Null);
    }
    
    [Fact]
    public void DeepDestruction_Propagation()
    {
        var (state, system) = CreateWorld();

        // P (Die) -> C1 (Die) -> C2 (Die)
        var p = state.CreateEntity();
        state.AddComponent(p, new Transform2D { Parent = new Entity(999), DestroyOnUnparent = true });
        
        var c1 = state.CreateEntity();
        state.AddComponent(c1, new Transform2D { Parent = p, DestroyOnUnparent = true });
        
        var c2 = state.CreateEntity();
        state.AddComponent(c2, new Transform2D { Parent = c1, DestroyOnUnparent = true });
        
        system.Update(state);
        
        state.HasComponent<Transform2D>(p).Should().BeFalse();
        state.HasComponent<Transform2D>(c1).Should().BeFalse();
        state.HasComponent<Transform2D>(c2).Should().BeFalse();
    }
}
