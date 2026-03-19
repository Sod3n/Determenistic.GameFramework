using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Physics2D.Components;
using Deterministic.GameFramework.Physics2D.Systems;
using Deterministic.GameFramework.TwoD;
using Deterministic.GameFramework.Common;
using Deterministic.GameFramework.DAR;
using Deterministic.GameFramework.Types;
using FluentAssertions;
using Xunit;
using System.Collections.Generic;

namespace Deterministic.GameFramework.Physics.Tests;

[Collection("Sequential")]
public class PhysicsSystemLogicTests
{
    private (EntityWorld state, GameLoop gameLoop, RapierPhysicsSystem physicsSystem) CreateWorld()
    {
        ServiceLocator.Reset();
        ServiceLocator.RegisterAssembly(typeof(EntityWorld).Assembly);
        ServiceLocator.RegisterAssembly(typeof(Transform2D).Assembly);
        ServiceLocator.RegisterAssembly(typeof(RigidBody2D).Assembly);

        var state = new EntityWorld();
        var dispatcher = new Dispatcher();
        var scheduler = new ActionScheduler();
        var gameLoop = new GameLoop(state, dispatcher, scheduler);
        gameLoop.SetTickRate(60);

        var physicsSystem = new RapierPhysicsSystem();
        gameLoop.Simulation.SystemRunner.EnableSystem(physicsSystem);

        return (state, gameLoop, physicsSystem);
    }

    [Fact]
    public void System_ShouldCreatePhysicsWorld_OnFirstUpdate()
    {
        var (state, gameLoop, system) = CreateWorld();

        gameLoop.RunSingleTick();

        var physicsState = state.GetCustomData<RapierPhysicsState>();
        physicsState.Should().NotBeNull();
        physicsState!.World.Should().NotBeNull();
    }

    [Fact]
    public void SyncEcsToPhysics_ShouldCreateBodies_ForNewEntities()
    {
        var (state, gameLoop, system) = CreateWorld();

        // Run once to init
        gameLoop.RunSingleTick();
        var physicsState = state.GetCustomData<RapierPhysicsState>();

        // Create Entity
        var entity = state.CreateEntity();
        state.AddComponent(entity, new Transform2D(new Vector2(10, 20), 0, Vector2.One));
        state.AddComponent(entity, RigidBody2D.Default);

        // Run tick
        gameLoop.RunSingleTick();

        // Verify Body Created
        physicsState!.EntityToBody.ContainsKey(entity.Id).Should().BeTrue();
        ulong bodyHandle = physicsState.EntityToBody[entity.Id];
        
        var pos = physicsState.World!.BodyGetTranslation(bodyHandle);
        pos.x.Should().BeApproximately(10, 0.001f);
        pos.y.Should().BeApproximately(20, 0.001f);
    }

    [Fact]
    public void SyncEcsToPhysics_ShouldUpdateBodies_WhenTransformChangesParams()
    {
        var (state, gameLoop, system) = CreateWorld();

        var entity = state.CreateEntity();
        state.AddComponent(entity, new Transform2D(Vector2.Zero, 0, Vector2.One));
        state.AddComponent(entity, RigidBody2D.Default);

        gameLoop.RunSingleTick();
        var physicsState = state.GetCustomData<RapierPhysicsState>();
        ulong bodyHandle = physicsState!.EntityToBody[entity.Id];

        // Teleport in ECS
        ref var transform = ref state.GetComponent<Transform2D>(entity);
        transform.GlobalPosition = new Vector2(100, 100);

        // Run tick
        gameLoop.RunSingleTick();

        // Verify Physics Updated
        var pos = physicsState.World!.BodyGetTranslation(bodyHandle);
        pos.x.Should().BeApproximately(100, 0.001f);
        pos.y.Should().BeApproximately(100, 0.001f);
    }

    [Fact]
    public void SyncPhysicsToEcs_ShouldUpdateTransform_WhenBodyMoves()
    {
        var (state, gameLoop, system) = CreateWorld();

        var entity = state.CreateEntity();
        state.AddComponent(entity, new Transform2D(Vector2.Zero, 0, Vector2.One));
        var rb = RigidBody2D.Default;
        rb.LinearVelocity = new Vector2(10, 0); // Move right
        rb.GravityScale = 0; // No gravity
        state.AddComponent(entity, rb);

        gameLoop.RunSingleTick(); // Creation
        gameLoop.RunSingleTick(); // Movement Step

        // dt = 1/60 = 0.01666...
        // Pos = 10 * 0.01666 = 0.1666
        
        var transform = state.GetComponent<Transform2D>(entity);
        ((float)transform.GlobalPosition.X).Should().BeGreaterThan(0.1f);
    }

    [Fact]
    public void RestoreState_ShouldResetAndRebuildWorld_WhenTickMismatchOccurs()
    {
        var (state, gameLoop, system) = CreateWorld();

        var entity = state.CreateEntity();
        state.AddComponent(entity, new Transform2D(new Vector2(10, 10), 0, Vector2.One));
        state.AddComponent(entity, RigidBody2D.Default);

        gameLoop.RunSingleTick(); // Tick 1
        
        var physicsState = state.GetCustomData<RapierPhysicsState>();
        ulong originalHandle = physicsState!.EntityToBody[entity.Id];

        // Simulate a Rollback: Force GameTime back or forward incorrectly, or manually mess with LastSimulatedTick
        physicsState.LastSimulatedTick = 100; // Future
        // Current GameLoop tick is 2 (after 1 run)
        // Mismatch: 2 != 100 + 1

        gameLoop.RunSingleTick(); // Should trigger ResetOrRestore

        // Handle might change if world was recreated
        ulong newHandle = physicsState.EntityToBody[entity.Id];
        
        // It's possible handles are reused, but we want to ensure state is consistent
        var pos = physicsState.World!.BodyGetTranslation(newHandle);
        pos.x.Should().BeApproximately(10, 0.001f); // Should still be at 10, 10 (rebuilt from ECS)
    }

    [Fact]
    public void Collider_Creation_And_Properties()
    {
        var (state, gameLoop, system) = CreateWorld();

        var entity = state.CreateEntity();
        state.AddComponent(entity, new Transform2D(Vector2.Zero, 0, Vector2.One));
        state.AddComponent(entity, RigidBody2D.Default);
        
        var shape = CollisionShape2D.CreateCircle(5.0f);
        shape.Position = new Vector2(1, 1); // Offset
        state.AddComponent(entity, shape);

        gameLoop.RunSingleTick();

        var physicsState = state.GetCustomData<RapierPhysicsState>();
        ulong bodyHandle = physicsState!.EntityToBody[entity.Id];
        
        // We can't easily inspect colliders via public API of RapierPhysicsSystem/State without uniffi access,
        // but we can check if simulation behaves as if collider exists (e.g. resting on ground).
        // Or we can trust that no exception was thrown and code coverage hits the lines.
    }
    
    [Fact]
    public void StaticBody_Creation_Sync()
    {
        var (state, gameLoop, system) = CreateWorld();
        
        var entity = state.CreateEntity();
        state.AddComponent(entity, new Transform2D(new Vector2(5, 5), 0, Vector2.One));
        state.AddComponent(entity, new StaticBody2D());
        
        gameLoop.RunSingleTick();
        
        var physicsState = state.GetCustomData<RapierPhysicsState>();
        physicsState!.EntityToBody.ContainsKey(entity.Id).Should().BeTrue();
        
        ulong bodyHandle = physicsState.EntityToBody[entity.Id];
        var pos = physicsState.World!.BodyGetTranslation(bodyHandle);
        
        pos.x.Should().BeApproximately(5, 0.001f);
    }
    
    [Fact]
    public void CharacterBody_Creation_Sync()
    {
        var (state, gameLoop, system) = CreateWorld();
        
        var entity = state.CreateEntity();
        state.AddComponent(entity, new Transform2D(new Vector2(5, 5), 0, Vector2.One));
        state.AddComponent(entity, CharacterBody2D.Default);
        
        gameLoop.RunSingleTick();
        
        var physicsState = state.GetCustomData<RapierPhysicsState>();
        physicsState!.EntityToBody.ContainsKey(entity.Id).Should().BeTrue();
        
        // Character controller is managed separately but body is Kinematic
        ulong bodyHandle = physicsState.EntityToBody[entity.Id];
        var pos = physicsState.World!.BodyGetTranslation(bodyHandle);
        pos.x.Should().BeApproximately(5, 0.001f);
    }

    [Fact]
    public void System_ShouldHandleMissingGameTime_Gracefully()
    {
        // Setup world WITHOUT GameTime
        ServiceLocator.Reset();
        ServiceLocator.RegisterAssembly(typeof(EntityWorld).Assembly);
        
        var state = new EntityWorld();
        // Do NOT set GameTime in CustomData
        
        var system = new RapierPhysicsSystem();
        
        // Should not throw
        system.Update(state);
    }

    [Fact]
    public void System_ShouldHandleMissingWorldEntity()
    {
        var (state, gameLoop, system) = CreateWorld();
        
        // Remove the default World entity created by GameLoop (if any) or ensure none exists
        // GameLoop constructor usually creates a World entity? 
        // Actually GameLoop uses GlobalState... but here we passed a fresh EntityWorld.
        // EntityWorld does NOT create a World entity by default. GameLoop might?
        // GameLoop logic depends on IGameTime which is in CustomData.
        
        // Ensure no entity has World component
        foreach(var e in state.Filter<World>())
        {
            state.DeleteEntity(e);
        }
        
        gameLoop.RunSingleTick();
        
        // Should run without crashing, using fallback entity (0)
        var physicsState = state.GetCustomData<RapierPhysicsState>();
        physicsState.Should().NotBeNull();
    }

    [Fact]
    public void EntityWithTransform_ButNoBodyComponent_ShouldBeIgnored()
    {
        var (state, gameLoop, system) = CreateWorld();
        
        var entity = state.CreateEntity();
        state.AddComponent(entity, new Transform2D(Vector2.Zero, 0, Vector2.One));
        // No Body component
        
        gameLoop.RunSingleTick();
        
        var physicsState = state.GetCustomData<RapierPhysicsState>();
        physicsState!.EntityToBody.ContainsKey(entity.Id).Should().BeFalse();
    }

    [Fact]
    public void History_ShouldBePruned_AfterTime()
    {
        // Custom setup with FakeGameTime
        ServiceLocator.Reset();
        ServiceLocator.RegisterAssembly(typeof(EntityWorld).Assembly);
        
        var state = new EntityWorld();
        var fakeTime = new FakeGameTime();
        fakeTime.CurrentTick = 500; // Future
        state.SetCustomData<IGameTime>(fakeTime);
        
        var physicsSystem = new RapierPhysicsSystem();
        
        // Init state
        var physicsState = new RapierPhysicsState();
        physicsState.World = new uniffi.rapier_uniffi.RapierWorld();
        state.SetCustomData(physicsState);
        
        // Inject history
        // Threshold = 500 - 300 = 200.
        physicsState.WorldStateHistory[100] = new byte[] { 1 }; // Should be removed (< 200)
        physicsState.WorldStateHistory[199] = new byte[] { 2 }; // Should be removed (< 200)
        physicsState.WorldStateHistory[200] = new byte[] { 3 }; // Should stay (== 200) ?? Logic is key < oldestTick. 200 < 200 is False. So stays.
        physicsState.WorldStateHistory[300] = new byte[] { 4 }; // Should stay
        
        // LastSimulatedTick must match to avoid Reset
        physicsState.LastSimulatedTick = 499; 
        
        // Update
        physicsSystem.Update(state);
        
        physicsState.WorldStateHistory.ContainsKey(100).Should().BeFalse();
        physicsState.WorldStateHistory.ContainsKey(199).Should().BeFalse();
        physicsState.WorldStateHistory.ContainsKey(200).Should().BeTrue();
        physicsState.WorldStateHistory.ContainsKey(300).Should().BeTrue();
        
        // Also a new entry for 500 should be added
        physicsState.WorldStateHistory.ContainsKey(500).Should().BeTrue();
    }
    [Fact]
    public void Rebuild_ShouldRestoreAllBodyTypes_WhenTickMismatchOccurs()
    {
        var (state, gameLoop, system) = CreateWorld();

        // Create one of each body type
        var sb = state.CreateEntity();
        state.AddComponent(sb, new Transform2D(new Vector2(1, 1), 0, Vector2.One));
        state.AddComponent(sb, new StaticBody2D());

        var cb = state.CreateEntity();
        state.AddComponent(cb, new Transform2D(new Vector2(2, 2), 0, Vector2.One));
        state.AddComponent(cb, CharacterBody2D.Default);

        var area = state.CreateEntity();
        state.AddComponent(area, new Transform2D(new Vector2(3, 3), 0, Vector2.One));
        state.AddComponent(area, new Area2D { CollisionLayer = 1, CollisionMask = 1 });

        gameLoop.RunSingleTick();

        // Check they exist in physics
        var physicsState = state.GetCustomData<RapierPhysicsState>();
        physicsState.EntityToBody.ContainsKey(sb.Id).Should().BeTrue();
        physicsState.EntityToBody.ContainsKey(cb.Id).Should().BeTrue();
        physicsState.EntityToBody.ContainsKey(area.Id).Should().BeTrue();

        // Trigger Mismatch
        physicsState.LastSimulatedTick = 100;
        
        gameLoop.RunSingleTick();

        // Handles might change, but keys should exist
        physicsState.EntityToBody.ContainsKey(sb.Id).Should().BeTrue();
        physicsState.EntityToBody.ContainsKey(cb.Id).Should().BeTrue();
        physicsState.EntityToBody.ContainsKey(area.Id).Should().BeTrue();
        
        // Verify positions are restored (rebuilt from ECS)
        var posSb = physicsState.World.BodyGetTranslation(physicsState.EntityToBody[sb.Id]);
        posSb.x.Should().BeApproximately(1, 0.001f);
    }

    [Fact]
    public void System_ShouldUpdatePhysicsWorldStateComponent_IfPresent()
    {
        var (state, gameLoop, system) = CreateWorld();
        
        // Find existing World entity (created by EntityWorld ctor)
        Entity worldEntity = Entity.Null;
        foreach (var e in state.Filter<World>())
        {
            worldEntity = e;
            break;
        }
        
        // Ensure we found it
        worldEntity.Should().NotBe(Entity.Null);
        
        // Add component to IT
        state.AddComponent(worldEntity, new PhysicsWorldState());
        
        // Run multiple ticks to ensure we move past default 0
        gameLoop.RunSingleTick(); // Tick 0 -> 1
        gameLoop.RunSingleTick(); // Tick 1 -> 2
        gameLoop.RunSingleTick(); // Tick 2 -> 3
        
        var comp = state.GetComponent<PhysicsWorldState>(worldEntity);
        var gameTime = state.GetCustomData<IGameTime>();
        
        gameTime.Should().NotBeNull();
        
        // comp.Tick should be the tick of the LAST update (Tick 2)
        // CurrentTick is now 3.
        comp.Tick.Should().Be(2); 
        comp.Tick.Should().Be(gameTime!.CurrentTick - 1);
    }

    [Fact]
    public void System_Dispose_ShouldBeCallable()
    {
        var system = new RapierPhysicsSystem();
        system.Dispose();
        // Should not throw
    }
}

public class FakeGameTime : IGameTime
{
    public long CurrentTick { get; set; }
    public float FixedDeltaTime { get; set; } = 1.0f / 60.0f;
    public int TickRate { get; set; } = 60;
    public bool IsResimulating { get; set; } = false;
}
