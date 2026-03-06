using Deterministic.GameFramework.CoreV2;
using Deterministic.GameFramework.Physics.Components;
using Deterministic.GameFramework.Physics.Systems;
using FluentAssertions;
using Xunit;

namespace Deterministic.GameFramework.Physics.Tests;

public class PhysicsCollisionTests
{
    private static (GlobalState state, GameLoop gameLoop) CreateWorldWithPhysics()
    {
        var state = new GlobalState();
        var dispatcher = new Dispatcher();
        var scheduler = new ActionScheduler();
        var gameLoop = new GameLoop(state, dispatcher, scheduler);
        gameLoop.SetTickRate(60);

        state.RegisterComponent<Transform2D>();
        state.RegisterComponent<RigidBody2D>();
        state.RegisterComponent<StaticBody2D>();
        state.RegisterComponent<CharacterBody2D>();
        state.RegisterComponent<CollisionShape2D>();
        state.RegisterComponent<PhysicsWorldState>();

        var physicsSystem = new RapierPhysicsSystem();
        gameLoop.RegisterSystem(physicsSystem);

        return (state, gameLoop);
    }

    [Fact]
    public void OverlappingDynamicBodies_ShouldBeSeparatedBySimulation()
    {
        var (state, gameLoop) = CreateWorldWithPhysics();

        var a = state.CreateEntity();
        var b = state.CreateEntity();

        // Both at (0,0) with Circle Radius 0.5
        state.AddComponent(a, new Transform2D(new Vector2(0, 0), 0, Vector2.One));
        state.AddComponent(b, new Transform2D(new Vector2(0, 0), 0, Vector2.One));

        state.AddComponent(a, RigidBody2D.Default);
        state.AddComponent(b, RigidBody2D.Default);
        
        state.AddComponent(a, CollisionShape2D.CreateCircle(0.5f));
        state.AddComponent(b, CollisionShape2D.CreateCircle(0.5f));

        for (int i = 0; i < 60; i++)
        {
            gameLoop.RunSingleTick();
        }

        var posA = state.GetComponent<Transform2D>(a).Position;
        var posB = state.GetComponent<Transform2D>(b).Position;

        posA.Should().NotBe(posB,
            "penetration resolution should separate overlapping dynamic bodies over time");
    }

    [Fact]
    public void DynamicBodyOverlappingStaticBody_ShouldMoveAwayWhileStaticStays()
    {
        var (state, gameLoop) = CreateWorldWithPhysics();

        var ground = state.CreateEntity();
        var body = state.CreateEntity();

        // Ground Static at (0,0)
        state.AddComponent(ground, new Transform2D(new Vector2(0, 0), 0, Vector2.One));
        state.AddComponent(ground, new StaticBody2D()); // Default is static
        state.AddComponent(ground, CollisionShape2D.CreateCircle(1.0f));

        // Body Dynamic at (0,0)
        state.AddComponent(body, new Transform2D(new Vector2(0, 0), 0, Vector2.One));
        state.AddComponent(body, RigidBody2D.Default);
        state.AddComponent(body, CollisionShape2D.CreateCircle(0.5f));

        for (int i = 0; i < 60; i++)
        {
            gameLoop.RunSingleTick();
        }

        var groundPos = state.GetComponent<Transform2D>(ground).Position;
        var bodyPos = state.GetComponent<Transform2D>(body).Position;

        groundPos.Should().Be(new Vector2(0, 0), "Static body should not move");
        bodyPos.Should().NotBe(groundPos,
            "collision resolution should separate a dynamic body from an overlapping static body over time");
    }

    [Fact]
    public void CharacterBody_ShouldCollideWithStaticBody()
    {
        var (state, gameLoop) = CreateWorldWithPhysics();

        var wall = state.CreateEntity();
        var character = state.CreateEntity();

        // Wall: Static, at (5, 0), Size (2, 10) -> x range [4, 6]
        // Rectangle center is at 5. Half-width is 1. Left edge is 4.
        state.AddComponent(wall, new Transform2D(new Vector2(5, 0), 0, Vector2.One));
        state.AddComponent(wall, new StaticBody2D());
        state.AddComponent(wall, CollisionShape2D.CreateRectangle(new Vector2(2, 10)));

        // Character: Kinematic, at (0, 0), Radius 0.5. Moving right (+X).
        // Target is past the wall.
        state.AddComponent(character, new Transform2D(new Vector2(0, 0), 0, Vector2.One));
        
        var charBody = CharacterBody2D.Default;
        charBody.Velocity = new Vector2(10, 0); // 10 units/sec
        state.AddComponent(character, charBody);
        
        state.AddComponent(character, CollisionShape2D.CreateCircle(0.5f));

        // Run for 1 second
        for (int i = 0; i < 60; i++)
        {
            gameLoop.RunSingleTick();
        }

        var charPos = state.GetComponent<Transform2D>(character).Position;

        // Wall left edge is 4.0. Character radius is 0.5.
        // Character center should stop at approx 3.5.
        // Allow some small margin/skin width.
        
        charPos.X.Should().BeLessThan(4.5f, "Character should not pass through the wall");
        charPos.X.Should().BeGreaterThan(3.0f, "Character should have moved up to the wall");
        
        var finalState = state.GetComponent<CharacterBody2D>(character);
        // We can't strictly assert IsOnWall unless we check normals, but it should be grounded/colliding?
        // Actually, rapier character controller 'grounded' usually implies 'down'.
        // IsOnWall logic might depend on slope angles.
        // For now, position check is sufficient to prove collision.
    }
}

