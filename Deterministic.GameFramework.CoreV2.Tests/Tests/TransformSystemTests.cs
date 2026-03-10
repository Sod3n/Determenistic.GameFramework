using Deterministic.GameFramework.CoreV2;
using Deterministic.GameFramework.CoreV2.Components;
using Deterministic.GameFramework.CoreV2.Systems;
using FluentAssertions;
using Xunit;

namespace Deterministic.GameFramework.CoreV2.Tests;

[Collection("Sequential")]
public class TransformSystemTests
{
    private (GlobalState state, TransformSystem system) Setup()
    {
        // ServiceLocator.Reset(); // Removed to avoid breaking parallel tests
        ServiceLocator.RegisterAssembly(typeof(Transform2D).Assembly);
        
        var state = new GlobalState();
        state.RegisterComponent<Transform2D>();
        var system = new TransformSystem();
        return (state, system);
    }

    [Fact]
    public void RootEntity_ShouldSyncWorldFromLocal()
    {
        var (state, system) = Setup();
        var entity = state.CreateEntity();
        
        state.AddComponent(entity, new Transform2D
        {
            Position = new Vector2(10, 20),
            Rotation = 90,
            Scale = new Vector2(2, 2)
        });

        system.Update(state);

        var transform = state.GetComponent<Transform2D>(entity);
        transform.GlobalPosition.Should().Be(new Vector2(10, 20));
        transform.GlobalRotation.Should().Be(90);
        transform.GlobalScale.Should().Be(new Vector2(2, 2));
    }

    [Fact]
    public void ChildEntity_ShouldFollowParentTranslation()
    {
        var (state, system) = Setup();
        
        var parent = state.CreateEntity();
        state.AddComponent(parent, new Transform2D { Position = new Vector2(100, 0), Scale = Vector2.One });
        
        var child = state.CreateEntity();
        state.AddComponent(child, new Transform2D 
        { 
            Parent = parent,
            Position = new Vector2(10, 0), // Relative to parent
            Scale = Vector2.One
        });

        system.Update(state);

        var childTransform = state.GetComponent<Transform2D>(child);
        childTransform.GlobalPosition.Should().Be(new Vector2(110, 0)); // 100 + 10
    }

    [Fact]
    public void ChildEntity_ShouldRotateAroundParent()
    {
        var (state, system) = Setup();
        
        var parent = state.CreateEntity();
        // Rotate parent 90 degrees (points Up)
        state.AddComponent(parent, new Transform2D 
        { 
            Position = Vector2.Zero,
            Rotation = new Float(MathF.PI / 2f), // 90 degrees in radians
            Scale = Vector2.One
        });
        
        var child = state.CreateEntity();
        // Child is at (10, 0) relative to parent. 
        // If parent rotates 90 deg, child should be at (0, 10) in world.
        state.AddComponent(child, new Transform2D 
        { 
            Parent = parent,
            Position = new Vector2(10, 0),
            Scale = Vector2.One
        });

        system.Update(state);

        var childTransform = state.GetComponent<Transform2D>(child);
        
        // Allow small float error
        ((float)childTransform.GlobalPosition.X).Should().BeApproximately(0f, 0.001f);
        ((float)childTransform.GlobalPosition.Y).Should().BeApproximately(10f, 0.001f);
        
        childTransform.GlobalRotation.Should().Be(new Float(MathF.PI / 2f)); // 0 + 90
    }

    [Fact]
    public void ChildEntity_ShouldScaleWithParent()
    {
        var (state, system) = Setup();
        
        var parent = state.CreateEntity();
        state.AddComponent(parent, new Transform2D 
        { 
            Position = Vector2.Zero,
            Scale = new Vector2(2, 2)
        });
        
        var child = state.CreateEntity();
        state.AddComponent(child, new Transform2D 
        { 
            Parent = parent,
            Position = new Vector2(10, 10), // Should become (20, 20)
            Scale = new Vector2(0.5f, 0.5f) // Final scale 2 * 0.5 = 1
        });

        system.Update(state);

        var childTransform = state.GetComponent<Transform2D>(child);
        childTransform.GlobalPosition.Should().Be(new Vector2(20, 20));
        childTransform.GlobalScale.Should().Be(Vector2.One);
    }

    [Fact]
    public void DeepHierarchy_ShouldPropagateTransforms()
    {
        var (state, system) = Setup();
        
        var root = state.CreateEntity();
        state.AddComponent(root, new Transform2D { Position = new Vector2(100, 0), Scale = Vector2.One });
        
        var mid = state.CreateEntity();
        state.AddComponent(mid, new Transform2D { Parent = root, Position = new Vector2(10, 0), Scale = Vector2.One });
        
        var leaf = state.CreateEntity();
        state.AddComponent(leaf, new Transform2D { Parent = mid, Position = new Vector2(1, 0), Scale = Vector2.One });

        system.Update(state);

        var leafTransform = state.GetComponent<Transform2D>(leaf);
        leafTransform.GlobalPosition.Should().Be(new Vector2(111, 0));
    }

    [Fact]
    public void ChangingParent_ShouldUpdateNextFrame()
    {
        var (state, system) = Setup();
        
        var parentA = state.CreateEntity();
        state.AddComponent(parentA, new Transform2D { Position = new Vector2(100, 0), Scale = Vector2.One });
        
        var parentB = state.CreateEntity();
        state.AddComponent(parentB, new Transform2D { Position = new Vector2(200, 0), Scale = Vector2.One });
        
        var child = state.CreateEntity();
        state.AddComponent(child, new Transform2D { Parent = parentA, Position = new Vector2(10, 0), Scale = Vector2.One });

        // Frame 1
        system.Update(state);
        state.GetComponent<Transform2D>(child).GlobalPosition.Should().Be(new Vector2(110, 0));

        // Switch Parent
        ref var childTransform = ref state.GetComponent<Transform2D>(child);
        childTransform.Parent = parentB;

        // Frame 2
        system.Update(state);
        state.GetComponent<Transform2D>(child).GlobalPosition.Should().Be(new Vector2(210, 0));
    }
    
    [Fact]
    public void OrphanedChild_ShouldActAsRoot()
    {
        var (state, system) = Setup();
        
        // Parent does not exist in ECS (never created)
        var fakeParent = new Entity(999);
        
        var child = state.CreateEntity();
        state.AddComponent(child, new Transform2D 
        { 
            Parent = fakeParent,
            Position = new Vector2(10, 10)
            // Default DestroyOnUnparent = false
        });

        system.Update(state);

        // Should treat as root (World = Local) because no DestroyOnUnparent
        state.HasComponent<Transform2D>(child).Should().BeTrue();
        var childTransform = state.GetComponent<Transform2D>(child);
        childTransform.GlobalPosition.Should().Be(new Vector2(10, 10));
    }

    [Fact]
    public void DeepHierarchy_ShouldDestroyAllInSingleFrame()
    {
        var (state, system) = Setup();
        
        var parent = state.CreateEntity();
        state.AddComponent(parent, new Transform2D { Position = Vector2.Zero });
        
        var child = state.CreateEntity();
        state.AddComponent(child, new Transform2D { Parent = parent, Position = Vector2.One, DestroyOnUnparent = true });
        
        var grandchild = state.CreateEntity();
        state.AddComponent(grandchild, new Transform2D { Parent = child, Position = Vector2.One, DestroyOnUnparent = true });
        
        // Frame 1: Valid
        system.Update(state);
        state.HasComponent<Transform2D>(grandchild).Should().BeTrue();
        
        // Destroy Parent
        state.DeleteEntity(parent);
        
        // Frame 2: Should destroy both Child AND Grandchild
        system.Update(state);
        
        state.HasComponent<Transform2D>(child).Should().BeFalse("Child should be destroyed");
        state.HasComponent<Transform2D>(grandchild).Should().BeFalse("Grandchild should be destroyed in the same frame");
    }

    [Fact]
    public void GlobalPositionChange_ShouldUpdateLocalPosition()
    {
        var (state, system) = Setup();
        
        var parent = state.CreateEntity();
        state.AddComponent(parent, new Transform2D { Position = new Vector2(100, 100), Scale = Vector2.One });
        
        var child = state.CreateEntity();
        state.AddComponent(child, new Transform2D 
        { 
            Parent = parent,
            Position = new Vector2(10, 10), // Initially at 110, 110
            Scale = Vector2.One
        });

        // Frame 1: Initial Sync
        system.Update(state);
        
        var childTransform = state.GetComponent<Transform2D>(child);
        childTransform.GlobalPosition.Should().Be(new Vector2(110, 110));

        // Modify Global Position manually
        childTransform.GlobalPosition = new Vector2(200, 200);
        state.AddComponent(child, childTransform); // Write back

        // Frame 2: Sync Back
        system.Update(state);

        childTransform = state.GetComponent<Transform2D>(child);
        
        // Global should match what we set
        childTransform.GlobalPosition.Should().Be(new Vector2(200, 200));
        
        // Local should be updated relative to parent (100, 100)
        // 200 - 100 = 100
        childTransform.Position.Should().Be(new Vector2(100, 100));
    }
}
