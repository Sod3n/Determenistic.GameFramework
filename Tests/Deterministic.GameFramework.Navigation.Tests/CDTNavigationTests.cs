using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Navigation2D.Components;
using Deterministic.GameFramework.Navigation2D.Systems;
using Deterministic.GameFramework.Physics2D.Components;
using Deterministic.GameFramework.Physics2D.Systems;
using Deterministic.GameFramework.TwoD;
using Deterministic.GameFramework.Common;
using Deterministic.GameFramework.Types;
using FluentAssertions;
using System.Linq;
using Xunit;

namespace Deterministic.GameFramework.Navigation.Tests;

/// <summary>
/// Tests for CDTNavigationSystem pathfinding.
/// Verifies agents respect obstacles and don't take shortcuts through walls.
/// </summary>
[Collection("Sequential")]
public class CDTNavigationTests
{
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

    private Entity CreateAgent(EntityWorld state, Vector2 position, Vector2 target)
    {
        var entity = state.CreateEntity();
        state.AddComponent(entity, new Transform2D(position, 0, Vector2.One));
        var agent = NavigationAgent2D.Default;
        agent.TargetPosition = target;
        agent.MaxSpeed = (Float)10;
        agent.TargetDesiredDistance = (Float)2;
        agent.PathDesiredDistance = (Float)1;
        agent.Radius = (Float)0.5f;
        agent.IsNavigationFinished = false;
        state.AddComponent(entity, agent);
        return entity;
    }

    [Fact]
    public void CDT_Map_WallCreatesConstraintEdges()
    {
        // Verify the CDT mesh actually includes the wall as an obstacle
        var (state, transformSystem, physicsSystem, cdtNavSystem) = CreateCDTWorld();

        CreateNavWorld(state, new Vector2(0, 0), new Vector2(200, 200), (Float)2);
        CreateWall(state, new Vector2(100, 100), new Vector2(10, 60));

        RunTick(state, transformSystem, physicsSystem, cdtNavSystem);

        var cdtState = state.GetCustomData<CDTNavigationState>();
        cdtState.Should().NotBeNull();
        cdtState!.Map.IsBuilt.Should().BeTrue();
        cdtState.Map.TriangleCount.Should().BeGreaterThan(2, "wall should cause additional triangles");
        cdtState.Map.ConstraintEdges.Should().NotBeEmpty("wall should create constraint edges");

        // Verify direct line through wall is NOT clear on navmesh
        var startOnMesh = cdtState.Map.IsSegmentOnNavMesh(new Vector2(30, 100), new Vector2(170, 100));
        startOnMesh.Should().BeFalse("direct line through wall should cross constraint edges");

        // Verify path around wall works
        int startTri = cdtState.Map.FindTriangle(new Vector2(30, 100));
        int endTri = cdtState.Map.FindTriangle(new Vector2(170, 100));
        startTri.Should().BeGreaterThanOrEqualTo(0);
        endTri.Should().BeGreaterThanOrEqualTo(0);

        var triPath = cdtState.Map.FindTrianglePath(startTri, endTri);
        triPath.Should().NotBeNull("should find a triangle path around the wall");
    }

    [Fact]
    public void CDT_Agent_PathExhausted_DoesNotGoThroughWall()
    {
        // Bug: when an agent's path waypoints are exhausted but it hasn't reached
        // the target, the fallback code steers directly to the target, ignoring walls.
        var (state, transformSystem, physicsSystem, cdtNavSystem) = CreateCDTWorld();

        CreateNavWorld(state, new Vector2(0, 0), new Vector2(200, 200), (Float)2);

        // Wall blocks direct path from left to right at x=100
        CreateWall(state, new Vector2(100, 100), new Vector2(10, 60));

        var agentEntity = CreateAgent(state, new Vector2(30, 100), new Vector2(170, 100));

        // Run initial ticks to bake and compute path
        RunTick(state, transformSystem, physicsSystem, cdtNavSystem);

        var cdtState = state.GetCustomData<CDTNavigationState>();
        cdtState.Should().NotBeNull();
        cdtState!.Map.IsBuilt.Should().BeTrue();

        ref var agent = ref state.GetComponent<NavigationAgent2D>(agentEntity);
        agent.IsTargetReachable.Should().BeTrue("agent should find a path around the wall");

        // Capture initial path after first tick
        string initialPathInfo = "";
        {
            var initAgent = state.GetComponent<NavigationAgent2D>(agentEntity);
            if (initAgent.PathPointCount > 0)
            {
                var parts = new List<string>();
                for (int i = 0; i < initAgent.PathPointCount; i++)
                {
                    var p = initAgent.PathPoints[i];
                    parts.Add($"({(float)p.X:F1},{(float)p.Y:F1})");
                }
                initialPathInfo = $"pathPts={initAgent.PathPointCount}: {string.Join(" -> ", parts)}";
            }
        }

        // Walk the agent, checking it never enters the wall zone
        bool enteredWallZone = false;
        Float closestToTarget = (Float)999999;
        string wallEntryInfo = "";

        for (int tick = 0; tick < 800; tick++)
        {
            RunTick(state, transformSystem, physicsSystem, cdtNavSystem);

            ref var t = ref state.GetComponent<Transform2D>(agentEntity);
            ref var a = ref state.GetComponent<NavigationAgent2D>(agentEntity);


            // Check: agent should never be inside the actual wall (x=95..105, y=70..130).
            // The path may clip wall corners closely — Rapier physics handles
            // the last-mile collision avoidance in the real game.
            var pos = t.GlobalPosition;
            if ((float)pos.X > 95f && (float)pos.X < 105f &&
                (float)pos.Y > 70f && (float)pos.Y < 130f)
            {
                if (!enteredWallZone)
                {
                    var curParts = new List<string>();
                    for (int i = 0; i < a.PathPointCount; i++)
                    {
                        var p = a.PathPoints[i];
                        curParts.Add($"({(float)p.X:F1},{(float)p.Y:F1})");
                    }
                    var curPts = curParts.Count > 0 ? string.Join(" -> ", curParts) : "none";
                    wallEntryInfo = $"tick={tick}, pos=({(float)pos.X:F1},{(float)pos.Y:F1}), vel=({(float)a.Velocity.X:F1},{(float)a.Velocity.Y:F1}), finished={a.IsNavigationFinished}, reachable={a.IsTargetReachable}, pathPts={a.PathPointCount}, pathIdx={a.PathIndex}, curPath=[{curPts}], initialPath=[{initialPathInfo}]";
                }
                enteredWallZone = true;
            }

            var dist = Vector2.Distance(pos, a.TargetPosition);
            if (dist < closestToTarget) closestToTarget = dist;
            if (a.IsNavigationFinished) break;
        }

        enteredWallZone.Should().BeFalse($"agent should never enter the actual wall. Entry: {wallEntryInfo}");
        closestToTarget.Should().BeLessThan((Float)50, "agent should get reasonably close to target by going around");
    }

    [Fact]
    public void CDT_Agent_PathExhaustedBehindWall_StopsInsteadOfGoingStraight()
    {
        // When path is consumed but target is behind a wall, the agent should
        // recompute or stop — not steer directly through the wall.
        var (state, transformSystem, physicsSystem, cdtNavSystem) = CreateCDTWorld();

        CreateNavWorld(state, new Vector2(0, 0), new Vector2(200, 200), (Float)2);

        // Full wall blocking x=100 (leaves only narrow gap at very top/bottom)
        CreateWall(state, new Vector2(100, 100), new Vector2(10, 180));

        var agentEntity = CreateAgent(state, new Vector2(30, 100), new Vector2(170, 100));

        // Run multiple ticks
        for (int tick = 0; tick < 200; tick++)
        {
            RunTick(state, transformSystem, physicsSystem, cdtNavSystem);

            ref var t = ref state.GetComponent<Transform2D>(agentEntity);
            ref var a = ref state.GetComponent<NavigationAgent2D>(agentEntity);

        }

        // Agent should never cross the wall
        ref var finalTransform = ref state.GetComponent<Transform2D>(agentEntity);
        ((float)finalTransform.GlobalPosition.X).Should().BeLessThan(95f,
            "agent should stay on its side of the wall and not steer through it");
    }

    [Fact]
    public void CDT_Agent_NavigatesAroundWall_ReachesTarget()
    {
        var (state, transformSystem, physicsSystem, cdtNavSystem) = CreateCDTWorld();

        CreateNavWorld(state, new Vector2(0, 0), new Vector2(200, 200), (Float)2);
        CreateWall(state, new Vector2(100, 100), new Vector2(10, 40));

        var agentEntity = CreateAgent(state, new Vector2(30, 100), new Vector2(170, 100));

        RunTick(state, transformSystem, physicsSystem, cdtNavSystem);

        ref var agent = ref state.GetComponent<NavigationAgent2D>(agentEntity);
        agent.IsTargetReachable.Should().BeTrue();



        Float closestToTarget = (Float)999999;
        for (int tick = 0; tick < 800; tick++)
        {
            RunTick(state, transformSystem, physicsSystem, cdtNavSystem);

            ref var t = ref state.GetComponent<Transform2D>(agentEntity);
            ref var a = ref state.GetComponent<NavigationAgent2D>(agentEntity);

            var pos = t.GlobalPosition;
            var dist = Vector2.Distance(pos, a.TargetPosition);
            if (dist < closestToTarget) closestToTarget = dist;
            if (a.IsNavigationFinished) break;
        }

        closestToTarget.Should().BeLessThan((Float)30, "agent should reach near the target by going around the wall");
    }

    [Fact]
    public void CDT_Agent_ObstacleSpawnedOnAgent_ShouldRecoverToNearestEdge()
    {
        // Scenario: agent is standing still, then an obstacle spawns on top of it.
        // Agent should walk to the nearest navmesh edge (short distance),
        // NOT teleport to a distant corner or run away far.
        var (state, transformSystem, physicsSystem, cdtNavSystem) = CreateCDTWorld();

        CreateNavWorld(state, new Vector2(0, 0), new Vector2(100, 100), (Float)1);

        // Place agent at center
        var agentEntity = CreateAgent(state, new Vector2(50, 50), new Vector2(50, 50));

        // Bake initial navmesh (no obstacles)
        RunTick(state, transformSystem, physicsSystem, cdtNavSystem);

        // Verify agent is on navmesh and idle
        ref var agent1 = ref state.GetComponent<NavigationAgent2D>(agentEntity);
        agent1.IsNavigationFinished = true;
        RunTick(state, transformSystem, physicsSystem, cdtNavSystem);

        var prePos = state.GetComponent<Transform2D>(agentEntity).GlobalPosition;

        // Spawn a 10x10 obstacle right on top of the agent (game buildings are this size)
        CreateWall(state, new Vector2(50, 50), new Vector2(10, 10));

        // Give target so agent has somewhere to go after recovery
        ref var agent2 = ref state.GetComponent<NavigationAgent2D>(agentEntity);
        agent2.TargetPosition = new Vector2(70, 50);
        agent2.IsNavigationFinished = false;

        // Let the system detect the change and rebake
        // (debounce is 10 ticks)
        for (int i = 0; i < 15; i++)
            RunTick(state, transformSystem, physicsSystem, cdtNavSystem);

        // Run recovery ticks — simulate game behavior: apply velocity to position
        Float maxDistFromStart = (Float)0;
        for (int tick = 0; tick < 120; tick++)
        {
            RunTick(state, transformSystem, physicsSystem, cdtNavSystem);

            ref var t = ref state.GetComponent<Transform2D>(agentEntity);
            ref var a = ref state.GetComponent<NavigationAgent2D>(agentEntity);

            // Simulate game physics: apply velocity to position (like CharacterBody2D would)
            t.Position += a.Velocity * (Float)1 / (Float)60;

            var pos = t.GlobalPosition;
            var distFromStart = Vector2.Distance(pos, prePos);
            if (distFromStart > maxDistFromStart)
                maxDistFromStart = distFromStart;
        }

        var finalPos = state.GetComponent<Transform2D>(agentEntity).GlobalPosition;
        var finalDistFromStart = Vector2.Distance(finalPos, prePos);

        System.Console.WriteLine($"RECOVERY: maxDist={(float)maxDistFromStart:F1} finalDist={(float)finalDistFromStart:F1} finalPos=({(float)finalPos.X:F0},{(float)finalPos.Y:F0})");

        // Agent should NOT have run far away — it should recover near the obstacle edge.
        // The obstacle is 6x6 (extends ±3 from center), with agentRadius=1 inflation
        // the navmesh hole is ~8x8. Nearest edge is ~4 units. Allow margin for path around.
        maxDistFromStart.Should().BeLessThan((Float)15,
            $"agent should recover near the obstacle, not run to a distant corner. " +
            $"Started at ({(float)prePos.X:F0},{(float)prePos.Y:F0}), " +
            $"ended at ({(float)finalPos.X:F0},{(float)finalPos.Y:F0}), " +
            $"max distance from start: {(float)maxDistFromStart:F1}");
    }
}
