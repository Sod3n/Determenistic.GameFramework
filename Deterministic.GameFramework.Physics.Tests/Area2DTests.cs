using System.Collections.Generic;
using Deterministic.GameFramework.CoreV2;
using Deterministic.GameFramework.CoreV2.Components;
using Deterministic.GameFramework.Physics.Components;
using Deterministic.GameFramework.Physics.Systems;
using FluentAssertions;
using Xunit;

namespace Deterministic.GameFramework.Physics.Tests;

[Collection("Sequential")]
public class Area2DTests
{
    private (GlobalState state, GameLoop gameLoop) CreateWorldWithPhysics()
    {
        // ServiceLocator.Reset(); // Removed to avoid breaking parallel tests
        ServiceLocator.RegisterAssembly(typeof(World).Assembly); // CoreV2
        ServiceLocator.RegisterAssembly(typeof(Area2D).Assembly); // Physics
        
        var state = new GlobalState();
        var dispatcher = new Dispatcher();
        var scheduler = new ActionScheduler();
        var gameLoop = new GameLoop(state, dispatcher, scheduler);
        gameLoop.SetTickRate(60);

        // Transform System runs before physics
        gameLoop.SystemRunner.EnableSystem(new Deterministic.GameFramework.CoreV2.Systems.TransformSystem());
        
        var physicsSystem = new RapierPhysicsSystem();
        gameLoop.SystemRunner.EnableSystem(physicsSystem);

        return (state, gameLoop);
    }

    [Fact]
    public void Area2D_ShouldDetectEnter_WhenEntityOverlaps()
    {
        var (state, gameLoop) = CreateWorldWithPhysics();

        // 1. Create Area at (0,0)
        var areaEntity = state.CreateEntity();
        state.AddComponent(areaEntity, new Transform2D(Vector2.Zero, 0, Vector2.One));
        state.AddComponent(areaEntity, CollisionShape2D.CreateCircle(2.0f)); // Radius 2
        state.AddComponent(areaEntity, new Area2D 
        { 
            Monitoring = true, 
            CollisionMask = uint.MaxValue 
        });

        // 2. Create Character outside Area at (10, 0)
        var charEntity = state.CreateEntity();
        state.AddComponent(charEntity, new Transform2D(new Vector2(10, 0), 0, Vector2.One));
        state.AddComponent(charEntity, CollisionShape2D.CreateCircle(0.5f));
        state.AddComponent(charEntity, RigidBody2D.Default); // Dynamic body

        // Tick 1: No overlap
        gameLoop.RunSingleTick();
        
        var area = state.GetComponent<Area2D>(areaEntity);
        area.HasOverlappingBodies.Should().BeFalse();

        // 3. Move Character Inside (0, 0)
        ref var charTransform = ref state.GetComponent<Transform2D>(charEntity);
        charTransform.LocalPosition = Vector2.Zero; // Update LocalPosition!
        // TransformSystem will update Position next frame. 
        // PhysicsSystem will update Body position from Transform next frame.
        
        // Tick 2: Overlap Detected
        gameLoop.RunSingleTick();

        area = state.GetComponent<Area2D>(areaEntity);
        area.HasOverlappingBodies.Should().BeTrue();
        area.OverlappingEntities.Count.Should().Be(1);
        area.OverlappingEntities[0].Should().Be(charEntity.Id);
        
        // Check EnteredMask (Bit 0 should be set)
        // 1 << 0 = 1
        (area.EnteredMask & 1).Should().NotBe(0, "EnteredMask should have bit 0 set");
        area.ExitedMask.Should().Be(0);
    }

    [Fact]
    public void Area2D_ShouldDetectStay_AndClearEnteredMask()
    {
        var (state, gameLoop) = CreateWorldWithPhysics();

        // 1. Setup Area and Character overlapping from start
        var areaEntity = state.CreateEntity();
        state.AddComponent(areaEntity, new Transform2D(Vector2.Zero, 0, Vector2.One));
        state.AddComponent(areaEntity, CollisionShape2D.CreateCircle(2.0f));
        state.AddComponent(areaEntity, new Area2D { Monitoring = true, CollisionMask = uint.MaxValue });

        var charEntity = state.CreateEntity();
        state.AddComponent(charEntity, new Transform2D(Vector2.Zero, 0, Vector2.One));
        state.AddComponent(charEntity, CollisionShape2D.CreateCircle(0.5f));
        state.AddComponent(charEntity, RigidBody2D.Default);

        // Tick 1: Enter
        gameLoop.RunSingleTick();
        
        var area = state.GetComponent<Area2D>(areaEntity);
        (area.EnteredMask & 1).Should().NotBe(0);

        // Tick 2: Stay
        gameLoop.RunSingleTick();

        area = state.GetComponent<Area2D>(areaEntity);
        area.HasOverlappingBodies.Should().BeTrue();
        area.OverlappingEntities.Count.Should().Be(1);
        
        // EnteredMask should be cleared
        (area.EnteredMask & 1).Should().Be(0, "EnteredMask should be cleared on subsequent frames");
        area.ExitedMask.Should().Be(0);
    }

    [Fact]
    public void Area2D_ShouldDetectExit_WhenEntityLeaves()
    {
        var (state, gameLoop) = CreateWorldWithPhysics();

        var areaEntity = state.CreateEntity();
        state.AddComponent(areaEntity, new Transform2D(Vector2.Zero, 0, Vector2.One));
        state.AddComponent(areaEntity, CollisionShape2D.CreateCircle(2.0f));
        state.AddComponent(areaEntity, new Area2D { Monitoring = true, CollisionMask = uint.MaxValue });

        var charEntity = state.CreateEntity();
        state.AddComponent(charEntity, new Transform2D(Vector2.Zero, 0, Vector2.One));
        state.AddComponent(charEntity, CollisionShape2D.CreateCircle(0.5f));
        state.AddComponent(charEntity, RigidBody2D.Default);

        // Tick 1: Enter
        gameLoop.RunSingleTick();

        // Move Character Away (10, 0)
        ref var charTransform = ref state.GetComponent<Transform2D>(charEntity);
        charTransform.LocalPosition = new Vector2(10, 0); // Update LocalPosition!

        // Tick 2: Exit
        gameLoop.RunSingleTick();

        var area = state.GetComponent<Area2D>(areaEntity);
        
        // On Exit frame, entity is STILL in the list, but ExitedMask is set
        area.OverlappingEntities.Count.Should().Be(1);
        area.OverlappingEntities[0].Should().Be(charEntity.Id);
        
        // ExitedMask bit 0 should be set
        (area.ExitedMask & 1).Should().NotBe(0, "ExitedMask should have bit 0 set");
        
        // Tick 3: Gone
        gameLoop.RunSingleTick();
        
        area = state.GetComponent<Area2D>(areaEntity);
        area.OverlappingEntities.Count.Should().Be(0);
        area.HasOverlappingBodies.Should().BeFalse();
        area.ExitedMask.Should().Be(0);
    }
}
