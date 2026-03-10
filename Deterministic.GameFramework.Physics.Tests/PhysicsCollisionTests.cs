using Deterministic.GameFramework.CoreV2;
using Deterministic.GameFramework.CoreV2.Components;
using Deterministic.GameFramework.Physics.Components;
using Deterministic.GameFramework.Physics.Systems;
using FluentAssertions;
using Xunit;

namespace Deterministic.GameFramework.Physics.Tests;

public class PhysicsCollisionTests
{
    private static (GlobalState state, GameLoop gameLoop) CreateWorldWithPhysics()
    {
        ServiceLocator.Reset();
        // Register assemblies first!
        ServiceLocator.RegisterAssembly(typeof(World).Assembly); // CoreV2
        ServiceLocator.RegisterAssembly(typeof(Area2D).Assembly); // Physics
        
        var state = new GlobalState();
        var dispatcher = new Dispatcher();
        var scheduler = new ActionScheduler();
        var gameLoop = new GameLoop(state, dispatcher, scheduler);
        gameLoop.SetTickRate(60);

        var physicsSystem = new RapierPhysicsSystem();
        gameLoop.SystemRunner.EnableSystem(physicsSystem);

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

    [Fact]
    public void CharacterBody_ShouldCollideWithOtherCharacterBody()
    {
        var (state, gameLoop) = CreateWorldWithPhysics();

        var charA = state.CreateEntity();
        var charB = state.CreateEntity();

        // Char A at (0, 0), moving Right (+X)
        state.AddComponent(charA, new Transform2D(new Vector2(0, 0), 0, Vector2.One));
        var bodyA = CharacterBody2D.Default;
        bodyA.Velocity = new Vector2(5, 0); 
        state.AddComponent(charA, bodyA);
        state.AddComponent(charA, CollisionShape2D.CreateCircle(0.5f));

        var checkA = state.GetComponent<CharacterBody2D>(charA);
        Console.WriteLine($"[Test] Init A: Vel={checkA.Velocity.X:F2} BodyId={checkA.BodyId}");

        // Char B at (4, 0), moving Left (-X)
        state.AddComponent(charB, new Transform2D(new Vector2(4, 0), 0, Vector2.One));
        var bodyB = CharacterBody2D.Default;
        bodyB.Velocity = new Vector2(-5, 0);
        state.AddComponent(charB, bodyB);
        state.AddComponent(charB, CollisionShape2D.CreateCircle(0.5f));

        var checkB = state.GetComponent<CharacterBody2D>(charB);
        Console.WriteLine($"[Test] Init B: Vel={checkB.Velocity.X:F2} BodyId={checkB.BodyId}");

        // Run for 1 second (60 ticks)
        for (int i = 0; i < 60; i++)
        {
            gameLoop.RunSingleTick();
            
            var pA = state.GetComponent<Transform2D>(charA).Position;
            var pB = state.GetComponent<Transform2D>(charB).Position;
            var bA = state.GetComponent<CharacterBody2D>(charA);
            var bB = state.GetComponent<CharacterBody2D>(charB);
            
            Console.WriteLine($"Tick {i}: A pos={pA.X:F2} vel={bA.Velocity.X:F2} realVel={bA.RealVelocity.X:F2}, B pos={pB.X:F2} vel={bB.Velocity.X:F2} realVel={bB.RealVelocity.X:F2}");
        }

        var posA = state.GetComponent<Transform2D>(charA).Position;
        var posB = state.GetComponent<Transform2D>(charB).Position;

        // Verify they didn't pass through each other
        posA.X.Should().BeLessThan(posB.X, "Char A should stay to the left of Char B");
        
        // Verify they collided (didn't reach target destinations of 5 and -1 if no collision)
        // Expected collision point around X=1.5 and X=2.5 (Centers)
        posA.X.Should().BeLessThan(2.0f, "Char A should have stopped before passing Char B");
        posB.X.Should().BeGreaterThan(2.0f, "Char B should have stopped before passing Char A");

        // Distance check
        var dist = Float.Abs(posB.X - posA.X);
        ((float)dist).Should().BeGreaterThanOrEqualTo(0.95f, "Characters should respect radius");
    }
}

