using BenchmarkDotNet.Attributes;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Physics2D.Components;
using Deterministic.GameFramework.Physics2D.Systems;
using Deterministic.GameFramework.TwoD;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Physics.Benchmarks;

[MemoryDiagnoser]
public class PhysicsSystemBenchmark
{
    private EntityWorld _state = null!;
    private RapierPhysicsSystem _system = null!;
    private FakeGameTime _gameTime = null!;

    [Params(10, 50, 100)]
    public int EntityCount;

    [GlobalSetup]
    public void Setup()
    {
        ComponentId.RegisterAssembly(typeof(PhysicsSystemBenchmark).Assembly);
        ComponentId.RegisterAssembly(typeof(World).Assembly);
        ComponentId.RegisterAssembly(typeof(Transform2D).Assembly);
        ComponentId.RegisterAssembly(typeof(RapierPhysicsSystem).Assembly);

        _state = new EntityWorld();
        _system = new RapierPhysicsSystem();
        _gameTime = new FakeGameTime { CurrentTick = 0 };
        _state.SetCustomData<IGameTime>(_gameTime);

        // Create static walls (4 walls like the game)
        CreateWall(new Vector2(25, -15), new Vector2(80, 1));
        CreateWall(new Vector2(25, 65), new Vector2(80, 1));
        CreateWall(new Vector2(-15, 25), new Vector2(1, 80));
        CreateWall(new Vector2(65, 25), new Vector2(1, 80));

        // Create dynamic bodies (RigidBody2D)
        int dynamicCount = EntityCount / 3;
        for (int i = 0; i < dynamicCount; i++)
        {
            var e = _state.CreateEntity();
            _state.AddComponent(e, new Transform2D(new Vector2(i * 2, 0), 0, Vector2.One));
            _state.AddComponent(e, new RigidBody2D
            {
                Mass = 1,
                GravityScale = 0,
                LinearVelocity = new Vector2(1, 0),
                CollisionLayer = 0xFFFF0001,
                CollisionMask = 0xFFFF0001
            });
            _state.AddComponent(e, CollisionShape2D.CreateCircle(0.5f));
        }

        // Create character bodies (CharacterBody2D)
        int charCount = EntityCount / 3;
        for (int i = 0; i < charCount; i++)
        {
            var e = _state.CreateEntity();
            _state.AddComponent(e, new Transform2D(new Vector2(i * 2, 10), 0, Vector2.One));
            _state.AddComponent(e, CharacterBody2D.Default);
            _state.AddComponent(e, CollisionShape2D.CreateCircle(0.5f));
        }

        // Create areas (Area2D)
        int areaCount = EntityCount - dynamicCount - charCount;
        for (int i = 0; i < areaCount; i++)
        {
            var e = _state.CreateEntity();
            _state.AddComponent(e, new Transform2D(new Vector2(i * 2, 20), 0, Vector2.One));
            _state.AddComponent(e, new Area2D { Monitoring = true, CollisionLayer = 1, CollisionMask = 0xFFFF0001 });
            _state.AddComponent(e, CollisionShape2D.CreateCircle(2f));
        }

        // Run one tick to initialize physics world
        _system.Update(_state);
        _gameTime.CurrentTick = 1;
    }

    [Benchmark]
    public void PhysicsUpdate()
    {
        _gameTime.CurrentTick++;
        _system.Update(_state);
    }

    [Benchmark]
    public void PhysicsUpdate_WithRebuild()
    {
        // Simulate a rollback by skipping a tick
        _gameTime.CurrentTick += 2;
        _system.Update(_state);
    }

    private void CreateWall(Vector2 position, Vector2 size)
    {
        var wall = _state.CreateEntity();
        _state.AddComponent(wall, new Transform2D(position, 0, Vector2.One));
        _state.AddComponent(wall, new StaticBody2D());
        _state.AddComponent(wall, CollisionShape2D.CreateRectangle(size));
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _system.Dispose();
    }
}

public class FakeGameTime : IGameTime
{
    public long CurrentTick { get; set; }
    public Float FixedDeltaTime { get; set; } = (Float)1 / (Float)60;
    public int TickRate { get; set; } = 60;
    public bool IsResimulating { get; set; } = false;
}
