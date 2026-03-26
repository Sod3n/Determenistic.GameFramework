using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Navigation2D.Components;
using Deterministic.GameFramework.Navigation2D.Systems;
using Deterministic.GameFramework.Physics2D.Components;
using Deterministic.GameFramework.TwoD;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Navigation.Benchmarks;

/// <summary>
/// Benchmarks the physics-based nav mesh bake path.
/// Single representative config: 30 obstacles, 5 agents, 4 regions.
/// </summary>
[SimpleJob(RuntimeMoniker.Net80, iterationCount: 5, warmupCount: 2)]
[MemoryDiagnoser]
public class PhysicsBakeBenchmark
{
    private EntityWorld _state = null!;
    private NavigationSystem _navSystem = null!;
    private TransformSystem _transformSystem = null!;
    private Entity _worldEntity;

    [GlobalSetup]
    public void Setup()
    {
        ComponentId.RegisterAssembly(typeof(PhysicsBakeBenchmark).Assembly);
        ComponentId.RegisterAssembly(typeof(World).Assembly);
        ComponentId.RegisterAssembly(typeof(Transform2D).Assembly);
        ComponentId.RegisterAssembly(typeof(NavigationRegion2D).Assembly);
        ComponentId.RegisterAssembly(typeof(StaticBody2D).Assembly);

        _state = new EntityWorld();
        _transformSystem = new TransformSystem();
        _navSystem = new NavigationSystem();

        // NavigationWorld2D: bounds (-500,-500) to (500,500), cellSize=8
        _worldEntity = _state.CreateEntity();
        _state.AddComponent(_worldEntity, NavigationWorld2D.Default);
        _state.AddComponent(_worldEntity, new Transform2D(Vector2.Zero, 0, Vector2.One));

        // 30 static body obstacles
        var rng = new Random(42);
        for (int i = 0; i < 30; i++)
        {
            var pos = new Vector2(
                (Float)(rng.NextDouble() * 800 - 400),
                (Float)(rng.NextDouble() * 800 - 400)
            );
            CreateStaticObstacle(pos, (Float)(10 + rng.NextDouble() * 40));
        }

        // 4 regions
        CreateSquareRegion(new Vector2(-200, -200), (Float)150);
        CreateSquareRegion(new Vector2(200, -200), (Float)150);
        CreateSquareRegion(new Vector2(-200, 200), (Float)150);
        CreateSquareRegion(new Vector2(200, 200), (Float)150);

        // 5 agents
        for (int i = 0; i < 5; i++)
        {
            Float t = (Float)i / (Float)4;
            CreateAgent(
                new Vector2((Float)(-400) + t * (Float)800, (Float)(-400)),
                new Vector2((Float)(400) - t * (Float)800, (Float)400));
        }

        _transformSystem.Update(_state);
        _navSystem.Update(_state);
    }

    [Benchmark(Baseline = true)]
    public void SteadyState()
    {
        _navSystem.Update(_state);
    }

    [Benchmark]
    public void PhysicsRebake()
    {
        ref var world = ref _state.GetComponent<NavigationWorld2D>(_worldEntity);
        world.ForceBake = true;
        _navSystem.Update(_state);
    }

    private void CreateStaticObstacle(Vector2 position, Float size)
    {
        var entity = _state.CreateEntity();
        _state.AddComponent(entity, new Transform2D(position, 0, Vector2.One));
        _state.AddComponent(entity, new StaticBody2D());
        _state.AddComponent(entity, CollisionShape2D.CreateRectangle(new Vector2(size, size)));
    }

    private void CreateSquareRegion(Vector2 position, Float size)
    {
        var entity = _state.CreateEntity();
        _state.AddComponent(entity, new Transform2D(position, 0, Vector2.One));
        var halfSize = size / (Float)2;
        var region = NavigationRegion2D.Default;
        region.VertexCount = 4;
        region.Vertices[0] = new Vector2(-halfSize, -halfSize);
        region.Vertices[1] = new Vector2(halfSize, -halfSize);
        region.Vertices[2] = new Vector2(halfSize, halfSize);
        region.Vertices[3] = new Vector2(-halfSize, halfSize);
        _state.AddComponent(entity, region);
    }

    private void CreateAgent(Vector2 position, Vector2 target)
    {
        var entity = _state.CreateEntity();
        _state.AddComponent(entity, new Transform2D(position, 0, Vector2.One));
        var agent = NavigationAgent2D.Default;
        agent.TargetPosition = target;
        agent.IsNavigationFinished = false;
        _state.AddComponent(entity, agent);
    }
}

/// <summary>
/// Compares chunked vs full rebake when a single obstacle is added.
/// Uses a moveable obstacle to trigger rebake without accumulating entities.
/// </summary>
[SimpleJob(RuntimeMoniker.Net80, iterationCount: 10, warmupCount: 3)]
[MemoryDiagnoser]
public class ChunkedVsFullBakeBenchmark
{
    private EntityWorld _chunkedState = null!;
    private EntityWorld _fullState = null!;
    private NavigationSystem _chunkedNav = null!;
    private NavigationSystem _fullNav = null!;
    private TransformSystem _transformSystem = null!;
    private Entity _chunkedMoveObstacle;
    private Entity _fullMoveObstacle;
    private int _iterCounter;

    [GlobalSetup]
    public void Setup()
    {
        ComponentId.RegisterAssembly(typeof(PhysicsBakeBenchmark).Assembly);
        ComponentId.RegisterAssembly(typeof(World).Assembly);
        ComponentId.RegisterAssembly(typeof(Transform2D).Assembly);
        ComponentId.RegisterAssembly(typeof(NavigationRegion2D).Assembly);
        ComponentId.RegisterAssembly(typeof(StaticBody2D).Assembly);

        _chunkedNav = new NavigationSystem();
        _fullNav = new NavigationSystem();
        _transformSystem = new TransformSystem();

        _chunkedState = CreateWorld(chunked: true, out _chunkedMoveObstacle);
        _fullState = CreateWorld(chunked: false, out _fullMoveObstacle);

        // Initial bake (includes first bake cost)
        _transformSystem.Update(_chunkedState);
        _chunkedNav.Update(_chunkedState);
        _transformSystem.Update(_fullState);
        _fullNav.Update(_fullState);

        // Second update to warm the chunked cache
        _chunkedNav.Update(_chunkedState);
        _fullNav.Update(_fullState);

        _iterCounter = 0;
    }

    private EntityWorld CreateWorld(bool chunked, out Entity moveObstacle)
    {
        var state = new EntityWorld();
        var worldEntity = state.CreateEntity();
        var world = NavigationWorld2D.Default;
        world.ChunkSize = chunked ? (Float)128 : (Float)0;
        state.AddComponent(worldEntity, world);
        state.AddComponent(worldEntity, new Transform2D(Vector2.Zero, 0, Vector2.One));

        // 30 static obstacles
        var rng = new Random(42);
        for (int i = 0; i < 30; i++)
        {
            var pos = new Vector2(
                (Float)(rng.NextDouble() * 800 - 400),
                (Float)(rng.NextDouble() * 800 - 400));
            var size = (Float)(10 + rng.NextDouble() * 40);
            var entity = state.CreateEntity();
            state.AddComponent(entity, new Transform2D(pos, 0, Vector2.One));
            state.AddComponent(entity, new StaticBody2D());
            state.AddComponent(entity, CollisionShape2D.CreateRectangle(new Vector2(size, size)));
        }

        // One moveable obstacle — we'll move this each iteration to trigger rebake
        moveObstacle = state.CreateEntity();
        state.AddComponent(moveObstacle, new Transform2D(new Vector2((Float)300, (Float)300), 0, Vector2.One));
        state.AddComponent(moveObstacle, new StaticBody2D());
        state.AddComponent(moveObstacle, CollisionShape2D.CreateRectangle(new Vector2((Float)15, (Float)15)));

        // 5 agents
        for (int i = 0; i < 5; i++)
        {
            Float t = (Float)i / (Float)4;
            var entity = state.CreateEntity();
            state.AddComponent(entity, new Transform2D(
                new Vector2((Float)(-400) + t * (Float)800, (Float)(-400)), 0, Vector2.One));
            var agent = NavigationAgent2D.Default;
            agent.TargetPosition = new Vector2((Float)(400) - t * (Float)800, (Float)400);
            state.AddComponent(entity, agent);
        }

        return state;
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _iterCounter++;
        // Move the obstacle to a new position to trigger rebake
        var newPos = new Vector2((Float)(300 + _iterCounter % 10), (Float)(300 + _iterCounter % 7));

        ref var ct = ref _chunkedState.GetComponent<Transform2D>(_chunkedMoveObstacle);
        ct.Position = newPos;
        ref var ft = ref _fullState.GetComponent<Transform2D>(_fullMoveObstacle);
        ft.Position = newPos;
    }

    [Benchmark(Baseline = true)]
    public void ChunkedBake_ObstacleMove()
    {
        _transformSystem.Update(_chunkedState);
        _chunkedNav.Update(_chunkedState);
    }

    [Benchmark]
    public void FullBake_ObstacleMove()
    {
        _transformSystem.Update(_fullState);
        _fullNav.Update(_fullState);
    }
}

/// <summary>
/// Benchmarks A* pathfinding allocation after pooling optimization.
/// Runs repeated pathfinds to show amortized allocation is near zero.
/// </summary>
[SimpleJob(RuntimeMoniker.Net80, iterationCount: 5, warmupCount: 2)]
[MemoryDiagnoser]
public class PathfindingAllocationBenchmark
{
    private EntityWorld _state = null!;
    private NavigationSystem _navSystem = null!;
    private TransformSystem _transformSystem = null!;

    [Params(1, 10, 50)]
    public int AgentCount;

    [GlobalSetup]
    public void Setup()
    {
        ComponentId.RegisterAssembly(typeof(PhysicsBakeBenchmark).Assembly);
        ComponentId.RegisterAssembly(typeof(World).Assembly);
        ComponentId.RegisterAssembly(typeof(Transform2D).Assembly);
        ComponentId.RegisterAssembly(typeof(NavigationRegion2D).Assembly);
        ComponentId.RegisterAssembly(typeof(StaticBody2D).Assembly);

        _state = new EntityWorld();
        _navSystem = new NavigationSystem();
        _transformSystem = new TransformSystem();

        // Physics world with obstacles for realistic triangle count
        var worldEntity = _state.CreateEntity();
        _state.AddComponent(worldEntity, NavigationWorld2D.Default);
        _state.AddComponent(worldEntity, new Transform2D(Vector2.Zero, 0, Vector2.One));

        var rng = new Random(42);
        for (int i = 0; i < 20; i++)
        {
            var pos = new Vector2(
                (Float)(rng.NextDouble() * 800 - 400),
                (Float)(rng.NextDouble() * 800 - 400));
            var entity = _state.CreateEntity();
            _state.AddComponent(entity, new Transform2D(pos, 0, Vector2.One));
            _state.AddComponent(entity, new StaticBody2D());
            _state.AddComponent(entity, CollisionShape2D.CreateRectangle(
                new Vector2((Float)(10 + rng.NextDouble() * 40), (Float)(10 + rng.NextDouble() * 40))));
        }

        for (int i = 0; i < AgentCount; i++)
        {
            Float t = AgentCount > 1 ? (Float)i / (Float)(AgentCount - 1) : (Float)0.5f;
            var entity = _state.CreateEntity();
            _state.AddComponent(entity, new Transform2D(
                new Vector2((Float)(-400) + t * (Float)800, (Float)(-400)), 0, Vector2.One));
            var agent = NavigationAgent2D.Default;
            agent.TargetPosition = new Vector2((Float)(400) - t * (Float)800, (Float)400);
            _state.AddComponent(entity, agent);
        }

        _transformSystem.Update(_state);
        _navSystem.Update(_state);
    }

    /// <summary>
    /// Force all agents to repath — measures per-tick allocation from A* + funnel.
    /// </summary>
    [Benchmark]
    public void AllAgentsRepath()
    {
        // Change every agent target to force recompute
        foreach (var entity in _state.Filter<NavigationAgent2D, Transform2D>())
        {
            ref var agent = ref _state.GetComponent<NavigationAgent2D>(entity);
            ref var transform = ref _state.GetComponent<Transform2D>(entity);
            agent.TargetPosition = transform.GlobalPosition + new Vector2((Float)50, (Float)50);
        }
        _navSystem.Update(_state);
    }
}
