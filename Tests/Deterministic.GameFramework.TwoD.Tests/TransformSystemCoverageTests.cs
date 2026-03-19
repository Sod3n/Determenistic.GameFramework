using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.TwoD;
using Deterministic.GameFramework.Common;
using Deterministic.GameFramework.Types;
using FluentAssertions;
using Xunit;
using System.Collections.Generic;

namespace Deterministic.GameFramework.TwoD.Tests;

[Collection("Sequential")]
public class TransformSystemCoverageTests
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
    public void OrphanedChild_Survives_And_BecomesRoot()
    {
        var (state, system) = CreateWorld();

        // Create a child with a Parent ID that does NOT exist in the world
        var child = state.CreateEntity();
        var fakeParentId = 9999;
        var parentEntity = new Entity(fakeParentId); // Doesn't exist in state
        
        var transform = new Transform2D(Vector2.Zero, 0, Vector2.One);
        transform.Parent = parentEntity;
        transform.DestroyOnUnparent = false; // Should survive
        
        state.AddComponent(child, transform);

        // Update should detect orphan
        system.Update(state);

        // Child should still exist
        state.HasComponent<Transform2D>(child).Should().BeTrue();
        
        // Child should now be root (Parent = Null)
        var updated = state.GetComponent<Transform2D>(child);
        updated.Parent.Should().Be(Entity.Null);
    }

    [Fact]
    public void OrphanedChild_Destroys_When_DestroyOnUnparent_True()
    {
        var (state, system) = CreateWorld();

        // Create a child with a Parent ID that does NOT exist
        var child = state.CreateEntity();
        var fakeParentId = 9999;
        var parentEntity = new Entity(fakeParentId);
        
        var transform = new Transform2D(Vector2.Zero, 0, Vector2.One);
        transform.Parent = parentEntity;
        transform.DestroyOnUnparent = true; // Should die
        
        state.AddComponent(child, transform);

        // Update should destroy
        system.Update(state);

        state.HasComponent<Transform2D>(child).Should().BeFalse();
    }

    [Fact]
    public void Child_GlobalPosition_Update_Calculates_LocalPosition()
    {
        var (state, system) = CreateWorld();

        var parent = state.CreateEntity();
        state.AddComponent(parent, new Transform2D(new Vector2(10, 10), 0, Vector2.One));

        var child = state.CreateEntity();
        var tChild = new Transform2D(new Vector2(5, 0), 0, Vector2.One);
        tChild.Parent = parent;
        state.AddComponent(child, tChild);

        // Initial Update to set baselines
        system.Update(state);
        
        var cInitial = state.GetComponent<Transform2D>(child);
        cInitial.GlobalPosition.Should().Be(new Vector2(15, 10)); // 10+5, 10+0
        
        // Modify Child GLOBAL position
        ref var cRef = ref state.GetComponent<Transform2D>(child);
        cRef.GlobalPosition = new Vector2(20, 10); // Moved 5 units right in world
        // Parent is at 10,10. So Local should become (10, 0).
        
        system.Update(state);
        
        var cUpdated = state.GetComponent<Transform2D>(child);
        cUpdated.Position.Should().Be(new Vector2(10, 0));
        cUpdated.GlobalPosition.Should().Be(new Vector2(20, 10));
    }

    [Fact]
    public void Deep_Hierarchy_Propagates_Transforms()
    {
        var (state, system) = CreateWorld();

        // Root -> Child1 -> Child2
        var root = state.CreateEntity();
        state.AddComponent(root, new Transform2D(new Vector2(10, 0), 0, Vector2.One));

        var child1 = state.CreateEntity();
        var t1 = new Transform2D(new Vector2(10, 0), 0, Vector2.One);
        t1.Parent = root;
        state.AddComponent(child1, t1);

        var child2 = state.CreateEntity();
        var t2 = new Transform2D(new Vector2(10, 0), 0, Vector2.One);
        t2.Parent = child1;
        state.AddComponent(child2, t2);

        system.Update(state);

        var c2 = state.GetComponent<Transform2D>(child2);
        c2.GlobalPosition.Should().Be(new Vector2(30, 0)); // 10 + 10 + 10
    }

    [Fact]
    public void Rotation_And_Scale_Propagation()
    {
        var (state, system) = CreateWorld();

        var parent = state.CreateEntity();
        // Rotate 90 degrees (Pi/2) and Scale 2x
        state.AddComponent(parent, new Transform2D(Vector2.Zero, Float.Pi / 2, new Vector2(2, 2)));

        var child = state.CreateEntity();
        // Child at (1, 0) local.
        // Rotated 90 deg -> (0, 1).
        // Scaled 2x -> (0, 2).
        var tChild = new Transform2D(new Vector2(1, 0), 0, Vector2.One);
        tChild.Parent = parent;
        state.AddComponent(child, tChild);

        system.Update(state);

        var c = state.GetComponent<Transform2D>(child);
        ((float)c.GlobalPosition.X).Should().BeApproximately(0, 0.001f);
        ((float)c.GlobalPosition.Y).Should().BeApproximately(2, 0.001f);
        ((float)c.GlobalRotation).Should().BeApproximately((float)(Float.Pi / 2), 0.001f);
        c.GlobalScale.Should().Be(new Vector2(2, 2));
    }
    
    [Fact]
    public void Child_Global_Update_With_Rotation_Untransforms_Correctly()
    {
        var (state, system) = CreateWorld();

        var parent = state.CreateEntity();
        // Parent at 0,0, Rotated 90 deg
        state.AddComponent(parent, new Transform2D(Vector2.Zero, Float.Pi / 2, Vector2.One));

        var child = state.CreateEntity();
        var tChild = new Transform2D(new Vector2(1, 0), 0, Vector2.One); // Starts at (0, 1) global
        tChild.Parent = parent;
        state.AddComponent(child, tChild);
        
        system.Update(state);
        
        // Move Child Global to (2, 0).
        // Relative to Parent (rotated 90):
        // Parent is facing +Y.
        // (2, 0) world is to the "Right" of parent.
        // In parent's local space (+X is Parent's Right? No, +X is local right).
        // Parent Rot 90: Local X -> World Y. Local Y -> World -X.
        // Wait:
        // Rot 90 (CCW):
        // Local (1, 0) -> World (0, 1). Correct.
        // We want World (2, 0).
        // This corresponds to Local (0, -2).
        // Local (0, -2) -> Rot 90 -> x= -(-2) = 2, y=0. Correct.
        
        ref var cRef = ref state.GetComponent<Transform2D>(child);
        cRef.GlobalPosition = new Vector2(2, 0);
        
        system.Update(state);
        
        var cUpdated = state.GetComponent<Transform2D>(child);
        ((float)cUpdated.Position.X).Should().BeApproximately(0, 0.001f);
        ((float)cUpdated.Position.Y).Should().BeApproximately(-2, 0.001f);
    }
}
