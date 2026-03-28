using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Physics2D.Components;
using Deterministic.GameFramework.Physics2D.Systems;
using Deterministic.GameFramework.TwoD;
using Deterministic.GameFramework.Common;
using Deterministic.GameFramework.Types;
using FluentAssertions;
using Xunit;
using System.Linq;

namespace Deterministic.GameFramework.Physics.Tests;

[Collection("Sequential")]
public class AreaLogicTests
{
    private (EntityWorld state, GameLoop gameLoop) CreateWorld()
    {
        ServiceLocator.Reset();
        ServiceLocator.RegisterAssembly(typeof(EntityWorld).Assembly);
        ServiceLocator.RegisterAssembly(typeof(Transform2D).Assembly);
        ServiceLocator.RegisterAssembly(typeof(Area2D).Assembly);

        var state = new EntityWorld();
        var dispatcher = new Deterministic.GameFramework.DAR.Dispatcher();
        var scheduler = new Deterministic.GameFramework.DAR.ActionScheduler();
        var gameLoop = new GameLoop(state, dispatcher, scheduler);
        gameLoop.SetTickRate(60);

        var physicsSystem = new RapierPhysicsSystem();
        gameLoop.Simulation.SystemRunner.EnableSystem(physicsSystem);

        return (state, gameLoop);
    }

    [Fact]
    public void Area2D_ShouldNotDetectOverlaps_WhenMonitoringIsFalse()
    {
        var (state, gameLoop) = CreateWorld();

        var areaEntity = state.CreateEntity();
        state.AddComponent(areaEntity, new Transform2D(Vector2.Zero, 0, Vector2.One));
        state.AddComponent(areaEntity, CollisionShape2D.CreateCircle(2.0f));
        state.AddComponent(areaEntity, new Area2D 
        { 
            Monitoring = false, // Disabled
            CollisionMask = uint.MaxValue 
        });

        var charEntity = state.CreateEntity();
        state.AddComponent(charEntity, new Transform2D(Vector2.Zero, 0, Vector2.One));
        state.AddComponent(charEntity, CollisionShape2D.CreateCircle(0.5f));
        state.AddComponent(charEntity, RigidBody2D.Default);

        gameLoop.RunSingleTick();

        var area = state.GetComponent<Area2D>(areaEntity);
        area.HasOverlappingBodies.Should().BeFalse();
        area.OverlappingEntities.Count.Should().Be(0);
    }

    [Fact]
    public void Area2D_Rectangle_ShouldDetectOverlap()
    {
        var (state, gameLoop) = CreateWorld();

        var areaEntity = state.CreateEntity();
        state.AddComponent(areaEntity, new Transform2D(Vector2.Zero, 0, Vector2.One));
        // Rectangle 4x4 centered at 0,0 (Extents 2,2)
        state.AddComponent(areaEntity, CollisionShape2D.CreateRectangle(new Vector2(4, 4))); 
        state.AddComponent(areaEntity, new Area2D { Monitoring = true, CollisionMask = uint.MaxValue });

        var charEntity = state.CreateEntity();
        // Position at (1.5, 0) -> Inside box (Edge is at 2.0)
        state.AddComponent(charEntity, new Transform2D(new Vector2(1.5f, 0), 0, Vector2.One));
        state.AddComponent(charEntity, CollisionShape2D.CreateCircle(0.1f));
        state.AddComponent(charEntity, RigidBody2D.Default);

        gameLoop.RunSingleTick();

        var area = state.GetComponent<Area2D>(areaEntity);
        area.HasOverlappingBodies.Should().BeTrue();
    }

    [Fact]
    public void Area2D_Capsule_ShouldDetectOverlap()
    {
        var (state, gameLoop) = CreateWorld();

        var areaEntity = state.CreateEntity();
        state.AddComponent(areaEntity, new Transform2D(Vector2.Zero, 0, Vector2.One));
        // Capsule Height 2, Radius 1. Vertical. Total height = 2 + 1*2 = 4.
        // Cylinder part from -1 to 1 Y. Caps extend to -2 and 2 Y. Width is 2 (-1 to 1 X).
        state.AddComponent(areaEntity, CollisionShape2D.CreateCapsule(1.0f, 2.0f)); 
        state.AddComponent(areaEntity, new Area2D { Monitoring = true, CollisionMask = uint.MaxValue });

        var charEntity = state.CreateEntity();
        // Position at (0, 1.5) -> Inside top cap
        state.AddComponent(charEntity, new Transform2D(new Vector2(0, 1.5f), 0, Vector2.One));
        state.AddComponent(charEntity, CollisionShape2D.CreateCircle(0.1f));
        state.AddComponent(charEntity, RigidBody2D.Default);

        gameLoop.RunSingleTick();

        var area = state.GetComponent<Area2D>(areaEntity);
        area.HasOverlappingBodies.Should().BeTrue();
    }

    [Fact]
    public void Area2D_ShouldRespectCollisionMask()
    {
        var (state, gameLoop) = CreateWorld();

        // Area listens to Layer 2 (Mask = 2)
        var areaEntity = state.CreateEntity();
        state.AddComponent(areaEntity, new Transform2D(Vector2.Zero, 0, Vector2.One));
        state.AddComponent(areaEntity, CollisionShape2D.CreateCircle(2.0f));
        state.AddComponent(areaEntity, new Area2D 
        { 
            Monitoring = true, 
            CollisionMask = 2 // Only Layer 2
        });

        // Char A: Layer 1 (Default) -> Should NOT match
        var charA = state.CreateEntity();
        state.AddComponent(charA, new Transform2D(Vector2.Zero, 0, Vector2.One));
        state.AddComponent(charA, CollisionShape2D.CreateCircle(0.5f));
        var rbA = RigidBody2D.Default;
        rbA.CollisionLayer = 1; 
        state.AddComponent(charA, rbA);

        // Char B: Layer 2 -> Should match
        var charB = state.CreateEntity();
        state.AddComponent(charB, new Transform2D(new Vector2(0.5f, 0), 0, Vector2.One));
        state.AddComponent(charB, CollisionShape2D.CreateCircle(0.5f));
        var rbB = RigidBody2D.Default;
        rbB.CollisionLayer = 2;
        state.AddComponent(charB, rbB);

        gameLoop.RunSingleTick();

        var area = state.GetComponent<Area2D>(areaEntity);
        area.HasOverlappingBodies.Should().BeTrue();
        area.OverlappingEntities.Count.Should().Be(1);
        area.OverlappingEntities[0].Should().Be(charB.Id);
    }
    
    [Fact]
    public void Area2D_ShouldSortMultipleOverlaps_ByEntityId()
    {
        var (state, gameLoop) = CreateWorld();
        
        var areaEntity = state.CreateEntity();
        state.AddComponent(areaEntity, new Transform2D(Vector2.Zero, 0, Vector2.One));
        state.AddComponent(areaEntity, CollisionShape2D.CreateCircle(5.0f));
        state.AddComponent(areaEntity, new Area2D { Monitoring = true, CollisionMask = uint.MaxValue });
        
        // Create 3 entities in random order of position, but IDs will be sequential
        var e1 = state.CreateEntity(); // ID 1
        var e2 = state.CreateEntity(); // ID 2
        var e3 = state.CreateEntity(); // ID 3
        
        state.AddComponent(e1, new Transform2D(new Vector2(1, 0), 0, Vector2.One));
        state.AddComponent(e1, CollisionShape2D.CreateCircle(0.5f));
        state.AddComponent(e1, RigidBody2D.Default);
        
        state.AddComponent(e2, new Transform2D(new Vector2(-1, 0), 0, Vector2.One));
        state.AddComponent(e2, CollisionShape2D.CreateCircle(0.5f));
        state.AddComponent(e2, RigidBody2D.Default);
        
        state.AddComponent(e3, new Transform2D(new Vector2(0, 1), 0, Vector2.One));
        state.AddComponent(e3, CollisionShape2D.CreateCircle(0.5f));
        state.AddComponent(e3, RigidBody2D.Default);
        
        gameLoop.RunSingleTick();
        
        var area = state.GetComponent<Area2D>(areaEntity);
        area.OverlappingEntities.Count.Should().Be(3);
        
        // Verify Sorted Order
        area.OverlappingEntities[0].Should().Be(e1.Id);
        area.OverlappingEntities[1].Should().Be(e2.Id);
        area.OverlappingEntities[2].Should().Be(e3.Id);
    }
}
