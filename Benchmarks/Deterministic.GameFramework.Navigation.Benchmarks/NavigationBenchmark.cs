using BenchmarkDotNet.Attributes;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Navigation2D.Components;
using Deterministic.GameFramework.Navigation2D.Systems;
using Deterministic.GameFramework.TwoD;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Navigation.Benchmarks;

[MemoryDiagnoser]
public class NavigationBenchmark
{
    private EntityWorld _state = null!;
    private NavigationSystem _navSystem = null!;
    private TransformSystem _transformSystem = null!;

    [Params(1, 10, 50)]
    public int AgentCount;

    [Params(1, 4)]
    public int RegionCount;

    [GlobalSetup]
    public void Setup()
    {
        ComponentId.RegisterAssembly(typeof(NavigationBenchmark).Assembly);
        ComponentId.RegisterAssembly(typeof(World).Assembly);
        ComponentId.RegisterAssembly(typeof(Transform2D).Assembly);
        ComponentId.RegisterAssembly(typeof(NavigationRegion2D).Assembly);

        _state = new EntityWorld();
        _transformSystem = new TransformSystem();
        _navSystem = new NavigationSystem();

        // Create navigation regions in a grid
        Float regionSize = (Float)100;
        int gridSize = (int)Math.Ceiling(Math.Sqrt(RegionCount));
        for (int i = 0; i < RegionCount; i++)
        {
            int gx = i % gridSize;
            int gy = i / gridSize;
            var pos = new Vector2((Float)(gx * 100 + 50), (Float)(gy * 100 + 50));
            CreateSquareRegion(pos, regionSize);
        }

        // Create agents spread across the nav mesh
        Float totalWidth = (Float)(gridSize * 100);
        for (int i = 0; i < AgentCount; i++)
        {
            Float t = AgentCount > 1 ? (Float)i / (Float)(AgentCount - 1) : (Float)0.5f;
            var startPos = new Vector2((Float)10 + t * (totalWidth - (Float)20), (Float)10);
            var targetPos = new Vector2(totalWidth - (Float)10 - t * (totalWidth - (Float)20), totalWidth - (Float)10);
            CreateAgent(startPos, targetPos);
        }

        // Initial update to build nav mesh
        _transformSystem.Update(_state);
        _navSystem.Update(_state);
    }

    [Benchmark]
    public void NavigationUpdate()
    {
        _navSystem.Update(_state);
    }

    [Benchmark]
    public void NavigationUpdate_WithRebuild()
    {
        // Force rebuild by modifying a region to trigger change detection
        foreach (var entity in _state.Filter<NavigationRegion2D, Transform2D>())
        {
            ref var region = ref _state.GetComponent<NavigationRegion2D>(entity);
            // Toggle a vertex slightly to force hash change
            var v = region.Vertices[0];
            region.Vertices[0] = new Vector2(v.X + Float.Epsilon, v.Y);
            break;
        }
        _navSystem.Update(_state);
    }

    [Benchmark]
    public void NavigationUpdate_TargetChanged()
    {
        // Change all agent targets to force path recompute
        foreach (var entity in _state.Filter<NavigationAgent2D, Transform2D>())
        {
            ref var agent = ref _state.GetComponent<NavigationAgent2D>(entity);
            ref var transform = ref _state.GetComponent<Transform2D>(entity);
            // Swap current position and target
            agent.TargetPosition = transform.GlobalPosition + new Vector2((Float)50, (Float)50);
        }

        _navSystem.Update(_state);
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
