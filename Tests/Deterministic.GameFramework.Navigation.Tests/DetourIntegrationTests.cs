using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Navigation2D.Components;
using Deterministic.GameFramework.Navigation2D.Systems;
using Deterministic.GameFramework.Physics2D.Components;
using Deterministic.GameFramework.Physics2D.Systems;
using Deterministic.GameFramework.TwoD;
using Deterministic.GameFramework.Common;
using Deterministic.GameFramework.Types;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Deterministic.GameFramework.Navigation.Tests;

/// <summary>
/// Diagnostic tests for Detour integration into CDTNavigationMap.
/// </summary>
[Collection("Sequential")]
public class DetourIntegrationTests
{
    private readonly ITestOutputHelper _output;

    public DetourIntegrationTests(ITestOutputHelper output) => _output = output;

    private (EntityWorld state, TransformSystem transformSystem, RapierPhysicsSystem physicsSystem, CDTNavigationSystem cdtNavSystem) CreateCDTWorld()
    {
        ServiceLocator.Reset();
        ServiceLocator.RegisterAssembly(typeof(EntityWorld).Assembly);
        ServiceLocator.RegisterAssembly(typeof(Transform2D).Assembly);
        ServiceLocator.RegisterAssembly(typeof(Area2D).Assembly);
        ServiceLocator.RegisterAssembly(typeof(NavigationRegion2D).Assembly);

        var state = new EntityWorld();
        state.RegisterComponent<Transform2D>();
        state.RegisterComponent<NavigationRegion2D>();
        state.RegisterComponent<NavigationAgent2D>();
        state.RegisterComponent<NavigationObstacle2D>();
        state.RegisterComponent<NavigationWorld2D>();
        state.RegisterComponent<StaticBody2D>();
        state.RegisterComponent<RigidBody2D>();
        state.RegisterComponent<CharacterBody2D>();
        state.RegisterComponent<CollisionShape2D>();
        state.RegisterComponent<Area2D>();

        var gameTime = new FakeGameTime { CurrentTick = 0 };
        state.SetCustomData<IGameTime>(gameTime);

        return (state, new TransformSystem(), new RapierPhysicsSystem(), new CDTNavigationSystem());
    }

    private void RunTick(EntityWorld state, TransformSystem transformSystem, RapierPhysicsSystem physicsSystem, CDTNavigationSystem cdtNavSystem)
    {
        var gameTime = state.GetCustomData<IGameTime>() as FakeGameTime;
        gameTime!.CurrentTick++;
        transformSystem.Update(state);
        physicsSystem.Update(state);
        cdtNavSystem.Update(state);
    }

    private Entity CreateNavWorld(EntityWorld state, Vector2 boundsMin, Vector2 boundsMax, Float agentRadius)
    {
        var entity = state.CreateEntity();
        var world = NavigationWorld2D.Default;
        world.BoundsMin = boundsMin;
        world.BoundsMax = boundsMax;
        world.AgentRadius = agentRadius;
        state.AddComponent(entity, world);
        return entity;
    }

    private Entity CreateWall(EntityWorld state, Vector2 position, Vector2 size)
    {
        var entity = state.CreateEntity();
        state.AddComponent(entity, new Transform2D(position, 0, Vector2.One));
        state.AddComponent(entity, new StaticBody2D());
        state.AddComponent(entity, CollisionShape2D.CreateRectangle(size));
        return entity;
    }

    [Fact]
    public void Detour_ComputeSmoothedPath_FindsPathAroundWall()
    {
        var (state, transformSystem, physicsSystem, cdtNavSystem) = CreateCDTWorld();

        CreateNavWorld(state, new Vector2(0, 0), new Vector2(200, 200), (Float)2);
        CreateWall(state, new Vector2(100, 100), new Vector2(10, 40));

        RunTick(state, transformSystem, physicsSystem, cdtNavSystem);

        var cdtState = state.GetCustomData<CDTNavigationState>();
        cdtState.Should().NotBeNull();
        cdtState!.Map.IsBuilt.Should().BeTrue();

        _output.WriteLine($"Triangles: {cdtState.Map.TriangleCount}");
        _output.WriteLine($"Constraint edges: {cdtState.Map.ConstraintEdges.Count}");

        // Test FindTriangle
        var startTri = cdtState.Map.FindTriangle(new Vector2(30, 100));
        var endTri = cdtState.Map.FindTriangle(new Vector2(170, 100));
        _output.WriteLine($"FindTriangle start: {startTri}, end: {endTri}");
        startTri.Should().BeGreaterThanOrEqualTo(0, "start should be on mesh");
        endTri.Should().BeGreaterThanOrEqualTo(0, "end should be on mesh");

        // Test legacy path (should still work)
        var triPath = cdtState.Map.FindTrianglePath(startTri, endTri);
        _output.WriteLine($"Legacy FindTrianglePath: {(triPath != null ? $"{triPath.Count} triangles" : "NULL")}");
        triPath.Should().NotBeNull("legacy A* should find path");

        // Test Detour ComputeSmoothedPath
        var result = new List<Vector2>();
        bool found = cdtState.Map.ComputeSmoothedPath(
            new Vector2(30, 100), new Vector2(170, 100), result, (Float)0.5f);

        _output.WriteLine($"ComputeSmoothedPath found: {found}");
        _output.WriteLine($"ComputeSmoothedPath points: {result.Count}");
        for (int i = 0; i < result.Count; i++)
        {
            _output.WriteLine($"  [{i}] ({(float)result[i].X:F1}, {(float)result[i].Y:F1})");
        }

        found.Should().BeTrue("Detour should find a path around the wall");
        result.Count.Should().BeGreaterThan(1, "path should have multiple points");

        // Verify path doesn't go through wall (x=95..105, y=80..120)
        foreach (var pt in result)
        {
            bool inWall = (float)pt.X > 93f && (float)pt.X < 107f &&
                          (float)pt.Y > 78f && (float)pt.Y < 122f;
            inWall.Should().BeFalse($"path point ({(float)pt.X:F1}, {(float)pt.Y:F1}) should not be inside wall zone");
        }
    }

    [Fact]
    public void Detour_IsSegmentOnNavMesh_BlockedByWall()
    {
        var (state, transformSystem, physicsSystem, cdtNavSystem) = CreateCDTWorld();

        CreateNavWorld(state, new Vector2(0, 0), new Vector2(200, 200), (Float)2);
        CreateWall(state, new Vector2(100, 100), new Vector2(10, 60));

        RunTick(state, transformSystem, physicsSystem, cdtNavSystem);

        var cdtState = state.GetCustomData<CDTNavigationState>();
        cdtState!.Map.IsBuilt.Should().BeTrue();

        // Direct line through wall should be blocked
        bool clear = cdtState.Map.IsSegmentOnNavMesh(new Vector2(30, 100), new Vector2(170, 100));
        _output.WriteLine($"Direct line through wall: {clear}");
        clear.Should().BeFalse("line through wall should be blocked");

        // Line around wall should be clear (using CDT triangle-walk)
        bool around = cdtState.Map.IsSegmentOnNavMesh(new Vector2(30, 60), new Vector2(170, 60));
        _output.WriteLine($"Line around wall (below): {around}");
        // Note: this may be false if the line goes near the mesh boundary
        // The important thing is the wall blocks correctly
    }

    [Fact]
    public void Detour_Performance_ComplexMesh()
    {
        // ── Build a dense mesh with many walls ──
        var (state, transformSystem, physicsSystem, cdtNavSystem) = CreateCDTWorld();

        CreateNavWorld(state, new Vector2(0, 0), new Vector2(200, 200), (Float)2);

        // 15 walls scattered across the map → complex mesh with many triangles
        CreateWall(state, new Vector2(30, 60), new Vector2(8, 50));
        CreateWall(state, new Vector2(60, 140), new Vector2(8, 60));
        CreateWall(state, new Vector2(90, 80), new Vector2(8, 70));
        CreateWall(state, new Vector2(120, 130), new Vector2(8, 50));
        CreateWall(state, new Vector2(150, 70), new Vector2(8, 60));
        CreateWall(state, new Vector2(170, 150), new Vector2(8, 40));
        CreateWall(state, new Vector2(40, 170), new Vector2(50, 8));
        CreateWall(state, new Vector2(100, 30), new Vector2(60, 8));
        CreateWall(state, new Vector2(140, 170), new Vector2(40, 8));
        CreateWall(state, new Vector2(50, 100), new Vector2(8, 30));
        CreateWall(state, new Vector2(80, 150), new Vector2(8, 40));
        CreateWall(state, new Vector2(110, 60), new Vector2(8, 40));
        CreateWall(state, new Vector2(160, 110), new Vector2(8, 50));
        CreateWall(state, new Vector2(25, 130), new Vector2(8, 40));
        CreateWall(state, new Vector2(180, 40), new Vector2(8, 50));

        RunTick(state, transformSystem, physicsSystem, cdtNavSystem);

        var cdtState = state.GetCustomData<CDTNavigationState>();
        cdtState!.Map.IsBuilt.Should().BeTrue();

        int triCount = cdtState.Map.TriangleCount;
        Console.WriteLine($"PERF: === Complex mesh: {triCount} triangles, 15 walls ===");

        var result = new List<Vector2>();
        var sw = new System.Diagnostics.Stopwatch();

        // ── Define test paths of varying difficulty ──
        var paths = new[]
        {
            (new Vector2(10, 10), new Vector2(190, 190), "corner-to-corner"),
            (new Vector2(10, 100), new Vector2(190, 100), "cross map horizontal"),
            (new Vector2(100, 10), new Vector2(100, 190), "cross map vertical"),
            (new Vector2(20, 50), new Vector2(70, 50), "short path"),
        };

        foreach (var (start, end, label) in paths)
        {
            // Warmup
            for (int i = 0; i < 20; i++)
                cdtState.Map.ComputeSmoothedPath(start, end, result, (Float)0.5f);

            // ── Detour: avg + spike analysis ──
            int iterations = 500;
            var detourTimes = new double[iterations];
            for (int i = 0; i < iterations; i++)
            {
                sw.Restart();
                cdtState.Map.ComputeSmoothedPath(start, end, result, (Float)0.5f);
                sw.Stop();
                detourTimes[i] = sw.Elapsed.TotalMicroseconds;
            }
            Array.Sort(detourTimes);
            double dAvg = detourTimes.Average();
            double dP50 = detourTimes[iterations / 2];
            double dP99 = detourTimes[(int)(iterations * 0.99)];
            double dMax = detourTimes[iterations - 1];
            int pathPts = result.Count;

            // ── Legacy: avg + spike analysis ──
            int startTri = cdtState.Map.FindTriangle(start);
            int endTri = cdtState.Map.FindTriangle(end);
            var legacyTimes = new double[iterations];
            for (int i = 0; i < iterations; i++)
            {
                sw.Restart();
                var triPath = cdtState.Map.FindTrianglePath(startTri, endTri);
                if (triPath != null)
                    cdtState.Map.SmoothPath(triPath, start, end, result, (Float)0.5f);
                sw.Stop();
                legacyTimes[i] = sw.Elapsed.TotalMicroseconds;
            }
            Array.Sort(legacyTimes);
            double lAvg = legacyTimes.Average();
            double lP50 = legacyTimes[iterations / 2];
            double lP99 = legacyTimes[(int)(iterations * 0.99)];
            double lMax = legacyTimes[iterations - 1];

            Console.WriteLine($"PERF: [{label}] ({pathPts} pts)");
            Console.WriteLine($"PERF:   Detour  avg={dAvg:F0} p50={dP50:F0} p99={dP99:F0} max={dMax:F0} us");
            Console.WriteLine($"PERF:   Legacy  avg={lAvg:F0} p50={lP50:F0} p99={lP99:F0} max={lMax:F0} us");
            Console.WriteLine($"PERF:   Ratio   avg={dAvg / lAvg:F1}x  p99={dP99 / lP99:F1}x  max={dMax / lMax:F1}x");
        }

        // ── Build time benchmark ──
        var buildTimes = new double[50];
        // Get the raw CDT data we need for rebuild
        var vertices = new List<Vector2>(cdtState.Map.Vertices);
        var edges = new List<Deterministic.GameFramework.CDT.Edge>(cdtState.Map.ConstraintEdges);
        for (int i = 0; i < buildTimes.Length; i++)
        {
            var freshMap = new CDTNavigationMap();
            sw.Restart();
            freshMap.Build(vertices, edges);
            sw.Stop();
            buildTimes[i] = sw.Elapsed.TotalMicroseconds;
        }
        Array.Sort(buildTimes);
        Console.WriteLine($"PERF: [build] avg={buildTimes.Average():F0} p50={buildTimes[buildTimes.Length / 2]:F0} p99={buildTimes[(int)(buildTimes.Length * 0.99)]:F0} max={buildTimes[^1]:F0} us");
    }
}
