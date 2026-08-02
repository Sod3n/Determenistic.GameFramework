using System.Diagnostics;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.TwoD;
using Deterministic.GameFramework.Types;
using Deterministic.GameFramework.Physics2D.Components;
using Deterministic.GameFramework.Physics2D.Systems;
using Deterministic.GameFramework.Common;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Deterministic.GameFramework.Physics.Tests;

[Collection("Sequential")]
public class Box2DVsRapierBenchmark
{
    private readonly ITestOutputHelper _output;
    public Box2DVsRapierBenchmark(ITestOutputHelper output) => _output = output;

    private EntityWorld CreateWorld()
    {
        ServiceLocator.Reset();
        ServiceLocator.RegisterAssembly(typeof(EntityWorld).Assembly);
        ServiceLocator.RegisterAssembly(typeof(Transform2D).Assembly);
        ServiceLocator.RegisterAssembly(typeof(Area2D).Assembly);

        var state = new EntityWorld();
        state.RegisterComponent<Transform2D>();
        state.RegisterComponent<StaticBody2D>();
        state.RegisterComponent<RigidBody2D>();
        state.RegisterComponent<CharacterBody2D>();
        state.RegisterComponent<CollisionShape2D>();
        state.RegisterComponent<Area2D>();

        state.SetCustomData<IGameTime>(new FakeGameTime { CurrentTick = 0 });
        return state;
    }

    private void PopulateScene(EntityWorld state, int staticCount, int dynamicCount, int characterCount)
    {
        // Static obstacles (buildings)
        for (int i = 0; i < staticCount; i++)
        {
            var e = state.CreateEntity();
            state.AddComponent(e, new Transform2D(new Vector2(i * 10, 0), 0, Vector2.One));
            state.AddComponent(e, new StaticBody2D());
            state.AddComponent(e, CollisionShape2D.CreateRectangle(new Vector2(4, 4)));
        }

        // Dynamic bodies (physics objects)
        for (int i = 0; i < dynamicCount; i++)
        {
            var e = state.CreateEntity();
            state.AddComponent(e, new Transform2D(new Vector2(i * 3, 20), 0, Vector2.One));
            var rb = new RigidBody2D { LinearVelocity = new Vector2((Float)(i % 5), (Float)(-(i % 3))) };
            state.AddComponent(e, rb);
            state.AddComponent(e, CollisionShape2D.CreateCircle((Float)0.5f));
        }

        // Character bodies (players/NPCs)
        for (int i = 0; i < characterCount; i++)
        {
            var e = state.CreateEntity();
            state.AddComponent(e, new Transform2D(new Vector2(i * 2, -10), 0, Vector2.One));
            var cb = new CharacterBody2D { Velocity = new Vector2((Float)(i % 3), (Float)1) };
            state.AddComponent(e, cb);
            state.AddComponent(e, CollisionShape2D.CreateCircle((Float)0.4f));
        }
    }

    private (double avgMs, double maxMs) BenchmarkSystem(ISystem system, EntityWorld state, int warmupTicks, int measureTicks)
    {
        var gameTime = state.GetCustomData<IGameTime>() as FakeGameTime;

        for (int i = 0; i < warmupTicks; i++)
        {
            gameTime!.CurrentTick++;
            system.Update(state);
        }

        var times = new List<double>();
        for (int i = 0; i < measureTicks; i++)
        {
            gameTime!.CurrentTick++;
            var sw = Stopwatch.StartNew();
            system.Update(state);
            sw.Stop();
            times.Add(sw.Elapsed.TotalMilliseconds);
        }

        return (times.Average(), times.Max());
    }

    [Fact]
    public void SmallScene_10Static_5Dynamic_2Character()
    {
        RunComparison("Small", 10, 5, 2, warmup: 10, measure: 60);
    }

    [Fact]
    public void MediumScene_50Static_20Dynamic_10Character()
    {
        RunComparison("Medium", 50, 20, 10, warmup: 10, measure: 60);
    }

    [Fact]
    public void LargeScene_100Static_50Dynamic_20Character()
    {
        RunComparison("Large", 100, 50, 20, warmup: 10, measure: 60);
    }

    [Fact]
    public void StressScene_200Static_100Dynamic_50Character()
    {
        RunComparison("Stress", 200, 100, 50, warmup: 5, measure: 30);
    }

    private void RunComparison(string label, int staticCount, int dynamicCount, int characterCount,
        int warmup, int measure)
    {
        // Rapier
        var rapierState = CreateWorld();
        PopulateScene(rapierState, staticCount, dynamicCount, characterCount);
        var rapierSystem = new RapierPhysicsSystem();
        var (rapierAvg, rapierMax) = BenchmarkSystem(rapierSystem, rapierState, warmup, measure);

        // Box2D
        var box2dState = CreateWorld();
        PopulateScene(box2dState, staticCount, dynamicCount, characterCount);
        var box2dSystem = new Box2DPhysicsSystem();
        var (box2dAvg, box2dMax) = BenchmarkSystem(box2dSystem, box2dState, warmup, measure);

        var speedup = rapierAvg / box2dAvg;

        _output.WriteLine($"=== {label} Scene ({staticCount}s + {dynamicCount}d + {characterCount}c) ===");
        _output.WriteLine($"  Rapier:  avg={rapierAvg:F3}ms  max={rapierMax:F3}ms");
        _output.WriteLine($"  Box2D:   avg={box2dAvg:F3}ms  max={box2dMax:F3}ms");
        _output.WriteLine($"  Speedup: {speedup:F1}x {(speedup > 1 ? "(Box2D faster)" : "(Rapier faster)")}");
    }
}
