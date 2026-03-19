using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Physics2D.Components;
using Deterministic.GameFramework.Physics2D.Systems;
using Deterministic.GameFramework.TwoD;
using Deterministic.GameFramework.Common;
using Deterministic.GameFramework.Types;
using FluentAssertions;
using Xunit;

namespace Deterministic.GameFramework.Physics.Tests;

[Collection("Sequential")]
public class CharacterBodyTests
{
    private (EntityWorld state, GameLoop gameLoop) CreateWorld()
    {
        ServiceLocator.Reset();
        ServiceLocator.RegisterAssembly(typeof(EntityWorld).Assembly);
        ServiceLocator.RegisterAssembly(typeof(Transform2D).Assembly);
        ServiceLocator.RegisterAssembly(typeof(CharacterBody2D).Assembly);

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
    public void CharacterBody_ShouldMove_WhenVelocityIsSet()
    {
        var (state, gameLoop) = CreateWorld();

        var entity = state.CreateEntity();
        state.AddComponent(entity, new Transform2D(Vector2.Zero, 0, Vector2.One));
        
        var character = CharacterBody2D.Default;
        character.Velocity = new Vector2(10, 0);
        state.AddComponent(entity, character);
        state.AddComponent(entity, CollisionShape2D.CreateCircle(0.5f));

        gameLoop.RunSingleTick(); // Tick 1 (Creation + Step)
        
        // StepCharacters runs before physics step using dt.
        // dt = 1/60.
        // Move = 10 * (1/60) = 0.1666...
        
        var transform = state.GetComponent<Transform2D>(entity);
        ((float)transform.GlobalPosition.X).Should().BeApproximately(0.1666f, 0.01f);
        
        var updatedChar = state.GetComponent<CharacterBody2D>(entity);
        ((float)updatedChar.RealVelocity.X).Should().BeApproximately(10f, 0.1f);
    }

    [Fact]
    public void CharacterBody_ShouldDetectFloor_WhenRestingOnStaticBody()
    {
        var (state, gameLoop) = CreateWorld();

        // Floor
        var floor = state.CreateEntity();
        state.AddComponent(floor, new Transform2D(new Vector2(0, -1), 0, Vector2.One)); // Top at y=0 (size 2 -> -1 +/- 1)
        state.AddComponent(floor, new StaticBody2D());
        state.AddComponent(floor, CollisionShape2D.CreateRectangle(new Vector2(10, 2))); // Width 10, Height 2

        // Character
        var entity = state.CreateEntity();
        // Place slightly above 0 (radius 0.5 -> bottom at 0.5)
        // Place at 0.51
        state.AddComponent(entity, new Transform2D(new Vector2(0, 0.51f), 0, Vector2.One));
        
        var character = CharacterBody2D.Default;
        character.Velocity = new Vector2(0, -1); // Move down
        character.FloorSnapLength = 0.1f;
        state.AddComponent(entity, character);
        state.AddComponent(entity, CollisionShape2D.CreateCircle(0.5f));

        // Run a few ticks to settle
        for (int i = 0; i < 5; i++)
        {
            gameLoop.RunSingleTick();
        }

        var updatedChar = state.GetComponent<CharacterBody2D>(entity);
        updatedChar.IsOnFloor.Should().BeTrue("Character should be on floor after moving down");
        
        var transform = state.GetComponent<Transform2D>(entity);
        // Should stop at 0.5 (radius)
        ((float)transform.GlobalPosition.Y).Should().BeApproximately(0.5f, 0.05f);
    }
}
