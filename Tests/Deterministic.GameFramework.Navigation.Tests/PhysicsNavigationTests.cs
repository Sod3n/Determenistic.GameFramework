using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Navigation2D.Components;
using Deterministic.GameFramework.Navigation2D.Systems;
using Deterministic.GameFramework.Physics2D.Components;
using Deterministic.GameFramework.Physics2D.Systems;
using Deterministic.GameFramework.TwoD;
using Deterministic.GameFramework.Common;
using Deterministic.GameFramework.Serialization;
using Deterministic.GameFramework.Types;
using FluentAssertions;
using Xunit;

namespace Deterministic.GameFramework.Navigation.Tests;

[Collection("Sequential")]
public class PhysicsNavigationTests
{
    private (EntityWorld state, TransformSystem transformSystem, RapierPhysicsSystem physicsSystem, NavigationSystem navSystem) CreatePhysicsWorld()
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

        return (state, new TransformSystem(), new RapierPhysicsSystem(), new NavigationSystem());
    }

    private void RunTick(EntityWorld state, TransformSystem transformSystem, RapierPhysicsSystem physicsSystem, NavigationSystem navSystem)
    {
        var gameTime = state.GetCustomData<IGameTime>() as FakeGameTime;
        gameTime!.CurrentTick++;
        transformSystem.Update(state);
        physicsSystem.Update(state);
        navSystem.Update(state);
    }

    private Entity CreateNavWorld(EntityWorld state, Vector2 boundsMin, Vector2 boundsMax, Float cellSize)
    {
        var entity = state.CreateEntity();
        var world = NavigationWorld2D.Default;
        world.BoundsMin = boundsMin;
        world.BoundsMax = boundsMax;
        world.CellSize = cellSize;
        world.AgentRadius = (Float)2;
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
        agent.IsNavigationFinished = false;
        state.AddComponent(entity, agent);
        return entity;
    }

    [Fact]
    public void NavigationWorld2D_CanBeRegistered()
    {
        var (state, _, _, _) = CreatePhysicsWorld();
        var entity = state.CreateEntity();
        state.AddComponent(entity, NavigationWorld2D.Default);
        state.HasComponent<NavigationWorld2D>(entity).Should().BeTrue();
    }

    [Fact]
    public void NavigationWorld2D_Default_HasCorrectValues()
    {
        var world = NavigationWorld2D.Default;
        ((float)world.CellSize).Should().BeApproximately(8f, 0.1f);
        ((float)world.AgentRadius).Should().BeApproximately(10f, 0.1f);
        world.ObstacleMask.Should().Be(uint.MaxValue);
        world.IncludeDynamicBodies.Should().BeFalse();
        world.ForceBake.Should().BeTrue();
    }

    [Fact]
    public void PhysicsBake_EmptyWorld_CreatesWalkableMesh()
    {
        var (state, transformSystem, physicsSystem, navSystem) = CreatePhysicsWorld();

        CreateNavWorld(state, new Vector2(0, 0), new Vector2(100, 100), (Float)10);

        // First tick to init physics world
        RunTick(state, transformSystem, physicsSystem, navSystem);

        // Check that a nav mesh was created
        var navState = state.GetCustomData<NavigationState>();
        navState.Should().NotBeNull();
        navState!.HasPhysicsBakedMesh.Should().BeTrue();
        navState.Map.Triangles.Count.Should().BeGreaterThan(0, "walkable mesh should have triangles");
    }

    [Fact]
    public void PhysicsBake_WithWall_CreatesPathAround()
    {
        var (state, transformSystem, physicsSystem, navSystem) = CreatePhysicsWorld();

        // Large area with small cell size for precision
        CreateNavWorld(state, new Vector2(0, 0), new Vector2(200, 200), (Float)8);

        // Short wall in the middle — leaves plenty of room above and below
        CreateWall(state, new Vector2(100, 100), new Vector2(10, 40));

        // Agent on the left, target on the right
        var agentEntity = CreateAgent(state, new Vector2(30, 100), new Vector2(170, 100));

        RunTick(state, transformSystem, physicsSystem, navSystem);

        var navState = state.GetCustomData<NavigationState>();
        navState!.HasPhysicsBakedMesh.Should().BeTrue("physics mesh should be baked");
        navState.Map.Triangles.Count.Should().BeGreaterThan(0, "nav mesh should have triangles");

        // Verify positions are findable in the mesh
        var startTri = navState.Map.FindTriangle(new Vector2(30, 100));
        var endTri = navState.Map.FindTriangle(new Vector2(170, 100));

        startTri.Should().BeGreaterThanOrEqualTo(0, $"start position should be in the nav mesh (triangles: {navState.Map.Triangles.Count})");
        endTri.Should().BeGreaterThanOrEqualTo(0, $"end position should be in the nav mesh (triangles: {navState.Map.Triangles.Count})");

        // Check adjacency connectivity
        int startAdjCount = navState.Map.Adjacency[startTri].Count;
        int endAdjCount = navState.Map.Adjacency[endTri].Count;

        startAdjCount.Should().BeGreaterThan(0, $"start triangle {startTri} should have neighbors");
        endAdjCount.Should().BeGreaterThan(0, $"end triangle {endTri} should have neighbors");

        // Check total adjacency connections
        int totalEdges = 0;
        for (int i = 0; i < navState.Map.Adjacency.Count; i++)
            totalEdges += navState.Map.Adjacency[i].Count;
        totalEdges.Should().BeGreaterThan(0, $"adjacency should have edges (triangles: {navState.Map.Triangles.Count})");

        // Check that A* finds a path
        var triPath = navState.Map.FindTrianglePath(startTri, endTri);
        triPath.Should().NotBeNull($"there should be a triangle path around the wall (start={startTri}, end={endTri}, adjEdges={totalEdges})");

        ref var agent = ref state.GetComponent<NavigationAgent2D>(agentEntity);
        agent.IsTargetReachable.Should().BeTrue("agent should find a path around the wall");
        agent.IsNavigationFinished.Should().BeFalse();
        agent.Velocity.Should().NotBe(Vector2.Zero);
    }

    [Fact]
    public void Agent_NavigatesAroundWall_ReachesTarget()
    {
        var (state, transformSystem, physicsSystem, navSystem) = CreatePhysicsWorld();

        CreateNavWorld(state, new Vector2(0, 0), new Vector2(200, 200), (Float)8);

        // Vertical wall in the center: blocks y=70..130 at x=100
        CreateWall(state, new Vector2(100, 100), new Vector2(10, 60));

        // Agent on the left, target on the right
        var agentEntity = CreateAgent(state, new Vector2(30, 100), new Vector2(170, 100));

        RunTick(state, transformSystem, physicsSystem, navSystem);

        ref var agent = ref state.GetComponent<NavigationAgent2D>(agentEntity);
        agent.IsTargetReachable.Should().BeTrue("agent should find a path around the wall");
        agent.Velocity.Should().NotBe(Vector2.Zero, "agent should start moving");

        // Walk the agent toward the target
        Float closestToTarget = (Float)999999;
        for (int tick = 0; tick < 600; tick++)
        {
            RunTick(state, transformSystem, physicsSystem, navSystem);

            ref var t = ref state.GetComponent<Transform2D>(agentEntity);
            ref var a = ref state.GetComponent<NavigationAgent2D>(agentEntity);
            t.Position += a.Velocity * (Float)1 / (Float)60;

            var dist = Vector2.Distance(t.GlobalPosition, a.TargetPosition);
            if (dist < closestToTarget) closestToTarget = dist;
            if (a.IsNavigationFinished) break;
        }

        closestToTarget.Should().BeLessThan((Float)30, "agent should reach near the target by going around the wall");
    }

    [Fact]
    public void Agent_NavigatesBetweenTwoWalls_ThroughGap()
    {
        var (state, transformSystem, physicsSystem, navSystem) = CreatePhysicsWorld();

        CreateNavWorld(state, new Vector2(0, 0), new Vector2(200, 200), (Float)8);

        // Two walls forming a corridor gap at y=95..105
        CreateWall(state, new Vector2(100, 50), new Vector2(10, 80));   // bottom wall: y=10..90
        CreateWall(state, new Vector2(100, 150), new Vector2(10, 80));  // top wall: y=110..190

        // Agent must go through the gap
        var agentEntity = CreateAgent(state, new Vector2(30, 100), new Vector2(170, 100));

        RunTick(state, transformSystem, physicsSystem, navSystem);

        ref var agent = ref state.GetComponent<NavigationAgent2D>(agentEntity);
        agent.IsTargetReachable.Should().BeTrue("there's a gap between the walls");

        // Walk the agent
        Float closestToTarget = (Float)999999;
        for (int tick = 0; tick < 600; tick++)
        {
            RunTick(state, transformSystem, physicsSystem, navSystem);

            ref var t = ref state.GetComponent<Transform2D>(agentEntity);
            ref var a = ref state.GetComponent<NavigationAgent2D>(agentEntity);
            t.Position += a.Velocity * (Float)1 / (Float)60;

            var dist = Vector2.Distance(t.GlobalPosition, a.TargetPosition);
            if (dist < closestToTarget) closestToTarget = dist;
            if (a.IsNavigationFinished) break;
        }

        closestToTarget.Should().BeLessThan((Float)30, "agent should navigate through the gap to reach the target");
    }

    [Fact]
    public void Agent_BlockedByFullWall_CannotReachTarget()
    {
        var (state, transformSystem, physicsSystem, navSystem) = CreatePhysicsWorld();

        CreateNavWorld(state, new Vector2(0, 0), new Vector2(200, 200), (Float)8);

        // Full wall spanning the entire height — no way around
        CreateWall(state, new Vector2(100, 100), new Vector2(10, 200));

        var agentEntity = CreateAgent(state, new Vector2(30, 100), new Vector2(170, 100));

        RunTick(state, transformSystem, physicsSystem, navSystem);

        ref var agent = ref state.GetComponent<NavigationAgent2D>(agentEntity);
        // Agent may not be able to reach target (path blocked or goes to closest point)
        // The key assertion: agent should NOT teleport to the other side
        ref var transform = ref state.GetComponent<Transform2D>(agentEntity);
        transform.GlobalPosition.X.Should().BeLessThan((Float)95,
            "agent should stay on its side of the wall");
    }

    [Fact]
    public void Agent_MultipleObstacles_FindsPath()
    {
        var (state, transformSystem, physicsSystem, navSystem) = CreatePhysicsWorld();

        CreateNavWorld(state, new Vector2(0, 0), new Vector2(300, 200), (Float)8);

        // Maze-like obstacles
        CreateWall(state, new Vector2(80, 60), new Vector2(10, 80));
        CreateWall(state, new Vector2(150, 140), new Vector2(10, 80));
        CreateWall(state, new Vector2(220, 60), new Vector2(10, 80));

        var agentEntity = CreateAgent(state, new Vector2(20, 100), new Vector2(280, 100));

        RunTick(state, transformSystem, physicsSystem, navSystem);

        ref var agent = ref state.GetComponent<NavigationAgent2D>(agentEntity);
        agent.IsTargetReachable.Should().BeTrue("there should be a navigable path through the obstacles");

        // Walk the agent
        Float closestToTarget = (Float)999999;
        for (int tick = 0; tick < 1000; tick++)
        {
            RunTick(state, transformSystem, physicsSystem, navSystem);

            ref var t = ref state.GetComponent<Transform2D>(agentEntity);
            ref var a = ref state.GetComponent<NavigationAgent2D>(agentEntity);
            t.Position += a.Velocity * (Float)1 / (Float)60;

            var dist = Vector2.Distance(t.GlobalPosition, a.TargetPosition);
            if (dist < closestToTarget) closestToTarget = dist;
            if (a.IsNavigationFinished) break;
        }

        closestToTarget.Should().BeLessThan((Float)40, "agent should navigate through multiple obstacles");
    }

    [Fact]
    public void PhysicsBake_AgentAvoidance_AutoEnabled()
    {
        var (state, transformSystem, physicsSystem, navSystem) = CreatePhysicsWorld();

        CreateNavWorld(state, new Vector2(0, 0), new Vector2(100, 100), (Float)10);

        var agentEntity = CreateAgent(state, new Vector2(10, 10), new Vector2(90, 90));

        RunTick(state, transformSystem, physicsSystem, navSystem);

        // Even though AvoidanceEnabled is false on the agent, physics nav auto-enables it
        ref var agent = ref state.GetComponent<NavigationAgent2D>(agentEntity);
        agent.AvoidanceEnabled.Should().BeFalse("the flag itself shouldn't change");
        // But velocity should be computed (avoidance is active via NavigationWorld2D)
        agent.Velocity.Should().NotBe(Vector2.Zero);
    }

    [Fact]
    public void PhysicsBake_WallAdded_RebakesAutomatically()
    {
        var (state, transformSystem, physicsSystem, navSystem) = CreatePhysicsWorld();

        CreateNavWorld(state, new Vector2(0, 0), new Vector2(100, 100), (Float)10);
        var agentEntity = CreateAgent(state, new Vector2(10, 50), new Vector2(90, 50));

        // Tick 1: no walls, path is straight
        RunTick(state, transformSystem, physicsSystem, navSystem);

        var navState = state.GetCustomData<NavigationState>();
        int triCountBefore = navState!.Map.Triangles.Count;

        // Add a wall
        CreateWall(state, new Vector2(50, 50), new Vector2(10, 100));

        // Tick 2: wall exists, should rebake
        RunTick(state, transformSystem, physicsSystem, navSystem);

        int triCountAfter = navState.Map.Triangles.Count;

        // The mesh should have changed (fewer walkable cells due to wall)
        triCountAfter.Should().NotBe(triCountBefore, "adding a wall should change the nav mesh");
    }

    [Fact]
    public void PhysicsBake_WithBoundaryWalls_AgentNavigatesInside()
    {
        var (state, transformSystem, physicsSystem, navSystem) = CreatePhysicsWorld();

        CreateNavWorld(state, new Vector2(-10, -10), new Vector2(110, 110), (Float)8);

        // Create boundary walls (like a room)
        CreateWall(state, new Vector2(50, -5), new Vector2(120, 10));   // Bottom
        CreateWall(state, new Vector2(50, 105), new Vector2(120, 10));  // Top
        CreateWall(state, new Vector2(-5, 50), new Vector2(10, 120));   // Left
        CreateWall(state, new Vector2(105, 50), new Vector2(10, 120));  // Right

        var agentEntity = CreateAgent(state, new Vector2(20, 20), new Vector2(80, 80));

        RunTick(state, transformSystem, physicsSystem, navSystem);

        ref var agent = ref state.GetComponent<NavigationAgent2D>(agentEntity);
        agent.IsTargetReachable.Should().BeTrue();
        agent.IsNavigationFinished.Should().BeFalse();
    }

    [Fact]
    public void PhysicsBake_NoNavigationWorld_DoesNotBake()
    {
        var (state, transformSystem, physicsSystem, navSystem) = CreatePhysicsWorld();

        // No NavigationWorld2D, just an agent
        var agentEntity = CreateAgent(state, new Vector2(10, 10), new Vector2(90, 90));

        RunTick(state, transformSystem, physicsSystem, navSystem);

        var navState = state.GetCustomData<NavigationState>();
        navState!.HasPhysicsBakedMesh.Should().BeFalse();
    }

    [Fact]
    public void PhysicsBake_ForceBake_ClearsAfterFirstFrame()
    {
        var (state, transformSystem, physicsSystem, navSystem) = CreatePhysicsWorld();

        var worldEntity = CreateNavWorld(state, new Vector2(0, 0), new Vector2(100, 100), (Float)10);

        RunTick(state, transformSystem, physicsSystem, navSystem);

        ref var world = ref state.GetComponent<NavigationWorld2D>(worldEntity);
        world.ForceBake.Should().BeFalse("ForceBake should be cleared after baking");
    }
}

/// <summary>
/// Tests that a CharacterBody2D agent navigating around a wall does not get stuck
/// on the wall corner. Reproduces a bug where nav velocity stays non-zero but
/// physics collision blocks actual movement, causing the agent to freeze.
/// </summary>
[Collection("Sequential")]
public class PhysicsNavigationObstacleTests
{
    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public PhysicsNavigationObstacleTests(Xunit.Abstractions.ITestOutputHelper output) => _output = output;

    private (EntityWorld state, TransformSystem transformSystem, RapierPhysicsSystem physicsSystem, NavigationSystem navSystem) CreatePhysicsWorld()
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

        return (state, new TransformSystem(), new RapierPhysicsSystem(), new NavigationSystem());
    }

    private void RunTick(EntityWorld state, TransformSystem transformSystem, RapierPhysicsSystem physicsSystem, NavigationSystem navSystem)
    {
        var gameTime = state.GetCustomData<IGameTime>() as FakeGameTime;
        gameTime!.CurrentTick++;
        transformSystem.Update(state);
        physicsSystem.Update(state);
        navSystem.Update(state);
    }

    private Entity CreateNavWorld(EntityWorld state, Vector2 boundsMin, Vector2 boundsMax, Float cellSize)
    {
        var entity = state.CreateEntity();
        var world = NavigationWorld2D.Default;
        world.BoundsMin = boundsMin;
        world.BoundsMax = boundsMax;
        world.CellSize = cellSize;
        world.AgentRadius = (Float)0.5f;
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

    private Entity CreateCharacterAgent(EntityWorld state, Vector2 position, Vector2 target)
    {
        var entity = state.CreateEntity();
        state.AddComponent(entity, new Transform2D(position, 0, Vector2.One));

        var body = CharacterBody2D.Default;
        body.CollisionLayer = 1;
        body.CollisionMask = uint.MaxValue;
        state.AddComponent(entity, body);
        state.AddComponent(entity, CollisionShape2D.CreateCircle((Float)0.5f));

        var agent = NavigationAgent2D.Default;
        agent.TargetPosition = target;
        agent.IsNavigationFinished = false;
        agent.MaxSpeed = (Float)10;
        agent.TargetDesiredDistance = (Float)2;
        agent.PathDesiredDistance = (Float)1;
        agent.Radius = (Float)0.5f;
        state.AddComponent(entity, agent);

        return entity;
    }

    [Fact]
    public void CharacterAgent_WithObstacleBetween_ShouldNavigateAroundAndReachTarget()
    {
        var (state, transformSystem, physicsSystem, navSystem) = CreatePhysicsWorld();

        // Nav world covering the test area
        CreateNavWorld(state, new Vector2(0, 0), new Vector2(50, 50), (Float)0.5f);

        // Horizontal wall blocking direct path: at Y=23, spanning X=14..30
        CreateWall(state, new Vector2(22, 23), new Vector2(16, 1));

        // Agent above the wall, target below the wall
        var agentEntity = CreateCharacterAgent(state, new Vector2(20, 20), new Vector2(20, 28));

        // Let physics + nav mesh bake
        for (int i = 0; i < 5; i++)
            RunTick(state, transformSystem, physicsSystem, navSystem);

        var startPos = state.GetComponent<Transform2D>(agentEntity).Position;

        _output.WriteLine($"=== CHARACTER BODY OBSTACLE NAVIGATION ===");
        _output.WriteLine($"Agent start: ({(float)startPos.X:F1}, {(float)startPos.Y:F1})");
        _output.WriteLine($"Target: (20.0, 28.0)");
        _output.WriteLine($"Wall: (22, 23) size (16, 1)");

        // Run simulation — CharacterBody2D velocity is applied by physics system
        for (int tick = 0; tick < 180; tick++)
        {
            // Apply nav velocity to character body (same as CowFollowSystem does)
            ref var nav = ref state.GetComponent<NavigationAgent2D>(agentEntity);
            ref var body = ref state.GetComponent<CharacterBody2D>(agentEntity);
            body.Velocity = nav.Velocity;

            RunTick(state, transformSystem, physicsSystem, navSystem);

            if (tick % 20 == 0 || tick < 5)
            {
                var pos = state.GetComponent<Transform2D>(agentEntity).Position;
                nav = ref state.GetComponent<NavigationAgent2D>(agentEntity);
                body = ref state.GetComponent<CharacterBody2D>(agentEntity);

                _output.WriteLine($"T{tick}: Pos=({(float)pos.X:F1},{(float)pos.Y:F1}) " +
                    $"NavVel=({(float)nav.Velocity.X:F2},{(float)nav.Velocity.Y:F2}) " +
                    $"BodyVel=({(float)body.Velocity.X:F2},{(float)body.Velocity.Y:F2}) " +
                    $"Finished={nav.IsNavigationFinished} Reachable={nav.IsTargetReachable} " +
                    $"Dist={Vector2.Distance(pos, nav.TargetPosition):F1}");
            }
        }

        var endPos = state.GetComponent<Transform2D>(agentEntity).Position;
        var target = new Vector2(20, 28);
        var initialDist = Vector2.Distance(startPos, target);
        var finalDist = Vector2.Distance(endPos, target);

        _output.WriteLine($"\n=== RESULT ===");
        _output.WriteLine($"Agent end: ({(float)endPos.X:F1}, {(float)endPos.Y:F1})");
        _output.WriteLine($"Dist: {initialDist:F1} -> {finalDist:F1}");

        finalDist.Should().BeLessThan(initialDist, "Agent should move closer to the target by navigating around the wall");
        ((float)finalDist).Should().BeLessThan(5f, "Agent should reach close to the target after navigating around the wall");
    }

    [Fact]
    public void CharacterAgent_DoesNotGetStuckOnWallCorner()
    {
        var (state, transformSystem, physicsSystem, navSystem) = CreatePhysicsWorld();

        CreateNavWorld(state, new Vector2(0, 0), new Vector2(50, 50), (Float)0.5f);

        // Wall that forces the agent to go around
        CreateWall(state, new Vector2(25, 25), new Vector2(20, 1));

        // Agent needs to cross from above to below the wall
        var agentEntity = CreateCharacterAgent(state, new Vector2(25, 20), new Vector2(25, 30));

        // Bake
        for (int i = 0; i < 5; i++)
            RunTick(state, transformSystem, physicsSystem, navSystem);

        // Track if agent ever stops making progress while nav thinks it's still moving
        int stuckTicks = 0;
        var prevPos = state.GetComponent<Transform2D>(agentEntity).Position;

        for (int tick = 0; tick < 300; tick++)
        {
            ref var nav = ref state.GetComponent<NavigationAgent2D>(agentEntity);
            ref var body = ref state.GetComponent<CharacterBody2D>(agentEntity);
            body.Velocity = nav.Velocity;

            RunTick(state, transformSystem, physicsSystem, navSystem);

            var pos = state.GetComponent<Transform2D>(agentEntity).Position;
            nav = ref state.GetComponent<NavigationAgent2D>(agentEntity);

            var moved = Vector2.Distance(pos, prevPos);
            bool navThinksMoved = nav.Velocity.SqrMagnitude > (Float)0.01f && !nav.IsNavigationFinished;

            if (navThinksMoved && (float)moved < 0.001f)
                stuckTicks++;
            else
                stuckTicks = 0; // reset consecutive stuck counter

            if (tick % 30 == 0)
            {
                _output.WriteLine($"T{tick}: Pos=({(float)pos.X:F1},{(float)pos.Y:F1}) " +
                    $"Moved={(float)moved:F3} NavVel=({(float)nav.Velocity.X:F1},{(float)nav.Velocity.Y:F1}) " +
                    $"Finished={nav.IsNavigationFinished} StuckStreak={stuckTicks}");
            }

            prevPos = pos;

            // Fail fast if stuck for too long
            stuckTicks.Should().BeLessThan(30,
                $"Agent should not get stuck on wall corner (stuck at ({(float)pos.X:F1},{(float)pos.Y:F1}) " +
                $"with nav velocity ({(float)nav.Velocity.X:F1},{(float)nav.Velocity.Y:F1}))");

            if (nav.IsNavigationFinished) break;
        }

        var endPos = state.GetComponent<Transform2D>(agentEntity).Position;
        var finalDist = Vector2.Distance(endPos, new Vector2(25, 30));
        _output.WriteLine($"\nFinal pos: ({(float)endPos.X:F1},{(float)endPos.Y:F1}) Dist to target: {finalDist:F1}");

        ((float)finalDist).Should().BeLessThan(5f, "Agent should reach the target");
    }
}

/// <summary>
/// Tests that smoothed paths do not cut through obstacles.
/// Reproduces a bug where the funnel algorithm pulls waypoints through blocked areas.
/// </summary>
[Collection("Sequential")]
public class PathThroughObstacleTests
{
    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public PathThroughObstacleTests(Xunit.Abstractions.ITestOutputHelper output) => _output = output;

    private (EntityWorld state, TransformSystem transformSystem, RapierPhysicsSystem physicsSystem, NavigationSystem navSystem) CreatePhysicsWorld()
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

        return (state, new TransformSystem(), new RapierPhysicsSystem(), new NavigationSystem());
    }

    private void RunTick(EntityWorld state, TransformSystem transformSystem, RapierPhysicsSystem physicsSystem, NavigationSystem navSystem)
    {
        var gameTime = state.GetCustomData<IGameTime>() as FakeGameTime;
        gameTime!.CurrentTick++;
        transformSystem.Update(state);
        physicsSystem.Update(state);
        navSystem.Update(state);
    }

    /// <summary>
    /// Check if a line segment from A to B passes through an AABB.
    /// </summary>
    private static bool SegmentIntersectsAABB(Vector2 a, Vector2 b, Vector2 boxMin, Vector2 boxMax)
    {
        // Liang-Barsky algorithm
        var d = b - a;
        Float tMin = (Float)0, tMax = (Float)1;

        Float[] p = { -d.X, d.X, -d.Y, d.Y };
        Float[] q = { a.X - boxMin.X, boxMax.X - a.X, a.Y - boxMin.Y, boxMax.Y - a.Y };

        for (int i = 0; i < 4; i++)
        {
            if ((float)p[i] == 0f)
            {
                if ((float)q[i] < 0f) return false; // parallel and outside
            }
            else
            {
                var t = q[i] / p[i];
                if ((float)p[i] < 0f)
                {
                    if (t > tMin) tMin = t;
                }
                else
                {
                    if (t < tMax) tMax = t;
                }
                if (tMin > tMax) return false;
            }
        }
        return true;
    }

    [Fact]
    public void SmoothedPath_ShouldNotCutThroughObstacle()
    {
        var (state, transformSystem, physicsSystem, navSystem) = CreatePhysicsWorld();

        // Nav world
        var worldEntity = state.CreateEntity();
        var world = NavigationWorld2D.Default;
        world.BoundsMin = new Vector2(0, 0);
        world.BoundsMax = new Vector2(100, 100);
        world.CellSize = (Float)2;
        world.AgentRadius = (Float)1;
        world.ChunkSize = (Float)0; // disable chunking for determinism
        state.AddComponent(worldEntity, world);

        // Wall in the center blocking direct path
        var wallPos = new Vector2(50, 50);
        var wallSize = new Vector2(4, 30);
        var wallEntity = state.CreateEntity();
        state.AddComponent(wallEntity, new Transform2D(wallPos, 0, Vector2.One));
        state.AddComponent(wallEntity, new StaticBody2D());
        state.AddComponent(wallEntity, CollisionShape2D.CreateRectangle(wallSize));

        // Agent on left, target on right — forces path around wall
        var agentEntity = state.CreateEntity();
        state.AddComponent(agentEntity, new Transform2D(new Vector2(20, 50), 0, Vector2.One));
        var agent = NavigationAgent2D.Default;
        agent.TargetPosition = new Vector2(80, 50);
        agent.IsNavigationFinished = false;
        agent.Radius = (Float)1;
        state.AddComponent(agentEntity, agent);

        // Bake and compute path
        RunTick(state, transformSystem, physicsSystem, navSystem);

        var navState = state.GetCustomData<NavigationState>();
        navState!.HasPhysicsBakedMesh.Should().BeTrue();

        // Get the computed path
        navState.AgentPaths.TryGetValue(agentEntity.Id, out var pathData).Should().BeTrue("agent should have a computed path");
        pathData!.PathPoints.Count.Should().BeGreaterThan(1, "path should have waypoints");

        _output.WriteLine($"Path has {pathData.PathPoints.Count} waypoints:");
        for (int i = 0; i < pathData.PathPoints.Count; i++)
        {
            var wp = pathData.PathPoints[i];
            _output.WriteLine($"  [{i}] ({(float)wp.X:F1}, {(float)wp.Y:F1})");
        }

        // The obstacle AABB (inflated by agent radius for safety)
        var obstacleMin = new Vector2(wallPos.X - wallSize.X / (Float)2, wallPos.Y - wallSize.Y / (Float)2);
        var obstacleMax = new Vector2(wallPos.X + wallSize.X / (Float)2, wallPos.Y + wallSize.Y / (Float)2);

        _output.WriteLine($"\nObstacle AABB: ({(float)obstacleMin.X:F1},{(float)obstacleMin.Y:F1}) to ({(float)obstacleMax.X:F1},{(float)obstacleMax.Y:F1})");

        // Check: no path segment should pass through the obstacle
        for (int i = 0; i < pathData.PathPoints.Count - 1; i++)
        {
            var from = pathData.PathPoints[i];
            var to = pathData.PathPoints[i + 1];
            bool cuts = SegmentIntersectsAABB(from, to, obstacleMin, obstacleMax);
            _output.WriteLine($"  Segment [{i}]→[{i+1}]: ({(float)from.X:F1},{(float)from.Y:F1})→({(float)to.X:F1},{(float)to.Y:F1}) cuts={cuts}");
            cuts.Should().BeFalse($"path segment [{i}]→[{i+1}] should not pass through obstacle " +
                $"(from ({(float)from.X:F1},{(float)from.Y:F1}) to ({(float)to.X:F1},{(float)to.Y:F1}))");
        }
    }

    [Fact]
    public void SmoothedPath_AgentNearWall_TargetBehindWall_ShouldRouteAround()
    {
        var (state, transformSystem, physicsSystem, navSystem) = CreatePhysicsWorld();

        var worldEntity = state.CreateEntity();
        var world = NavigationWorld2D.Default;
        world.BoundsMin = new Vector2(0, 0);
        world.BoundsMax = new Vector2(60, 60);
        world.CellSize = (Float)1;
        world.AgentRadius = (Float)1;
        world.ChunkSize = (Float)0;
        state.AddComponent(worldEntity, world);

        // Horizontal wall: blocks y=28..32 across x=15..45
        var wallPos = new Vector2(30, 30);
        var wallSize = new Vector2(30, 4);
        var wallEntity = state.CreateEntity();
        state.AddComponent(wallEntity, new Transform2D(wallPos, 0, Vector2.One));
        state.AddComponent(wallEntity, new StaticBody2D());
        state.AddComponent(wallEntity, CollisionShape2D.CreateRectangle(wallSize));

        // Agent just above the wall, target just below — must go around
        var agentEntity = state.CreateEntity();
        state.AddComponent(agentEntity, new Transform2D(new Vector2(30, 26), 0, Vector2.One));
        var agent = NavigationAgent2D.Default;
        agent.TargetPosition = new Vector2(30, 34);
        agent.IsNavigationFinished = false;
        agent.Radius = (Float)1;
        state.AddComponent(agentEntity, agent);

        RunTick(state, transformSystem, physicsSystem, navSystem);

        var navState = state.GetCustomData<NavigationState>();
        navState!.AgentPaths.TryGetValue(agentEntity.Id, out var pathData).Should().BeTrue();

        _output.WriteLine($"Path has {pathData!.PathPoints.Count} waypoints:");
        for (int i = 0; i < pathData.PathPoints.Count; i++)
        {
            var wp = pathData.PathPoints[i];
            _output.WriteLine($"  [{i}] ({(float)wp.X:F1}, {(float)wp.Y:F1})");
        }

        var obstacleMin = new Vector2(wallPos.X - wallSize.X / (Float)2, wallPos.Y - wallSize.Y / (Float)2);
        var obstacleMax = new Vector2(wallPos.X + wallSize.X / (Float)2, wallPos.Y + wallSize.Y / (Float)2);

        // No segment should cut through the wall
        for (int i = 0; i < pathData.PathPoints.Count - 1; i++)
        {
            var from = pathData.PathPoints[i];
            var to = pathData.PathPoints[i + 1];
            bool cuts = SegmentIntersectsAABB(from, to, obstacleMin, obstacleMax);
            cuts.Should().BeFalse($"path segment [{i}]→[{i+1}] cuts through wall " +
                $"(from ({(float)from.X:F1},{(float)from.Y:F1}) to ({(float)to.X:F1},{(float)to.Y:F1}))");
        }

        // Path should have more than 2 waypoints (can't be a straight line)
        pathData.PathPoints.Count.Should().BeGreaterThan(2,
            "path must route around the wall, so it needs intermediate waypoints");
    }
}

/// <summary>
/// Regression test: agent near a wall with target directly behind (same axis, opposite side)
/// should build a path that does not cut through the wall body and should not get stuck.
/// </summary>
[Collection("Sequential")]
public class PathNearObstacleRegressionTests
{
    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public PathNearObstacleRegressionTests(Xunit.Abstractions.ITestOutputHelper output) => _output = output;

    private (EntityWorld state, TransformSystem transformSystem, RapierPhysicsSystem physicsSystem, NavigationSystem navSystem) CreatePhysicsWorld()
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

        return (state, new TransformSystem(), new RapierPhysicsSystem(), new NavigationSystem());
    }

    private void RunTick(EntityWorld state, TransformSystem transformSystem, RapierPhysicsSystem physicsSystem, NavigationSystem navSystem)
    {
        var gameTime = state.GetCustomData<IGameTime>() as FakeGameTime;
        gameTime!.CurrentTick++;
        transformSystem.Update(state);
        physicsSystem.Update(state);
        navSystem.Update(state);
    }

    /// <summary>Check if a line segment intersects an AABB (Liang-Barsky).</summary>
    private static bool SegmentIntersectsAABB(Vector2 a, Vector2 b, Vector2 boxMin, Vector2 boxMax)
    {
        var d = b - a;
        Float tMin = (Float)0, tMax = (Float)1;
        Float[] p = { -d.X, d.X, -d.Y, d.Y };
        Float[] q = { a.X - boxMin.X, boxMax.X - a.X, a.Y - boxMin.Y, boxMax.Y - a.Y };
        for (int i = 0; i < 4; i++)
        {
            if ((float)p[i] == 0f) { if ((float)q[i] < 0f) return false; }
            else
            {
                var t = q[i] / p[i];
                if ((float)p[i] < 0f) { if (t > tMin) tMin = t; }
                else { if (t < tMax) tMax = t; }
                if (tMin > tMax) return false;
            }
        }
        return true;
    }

    [Fact]
    public void AgentNearWall_TargetStraightBehind_PathDoesNotCrossWall()
    {
        var (state, transformSystem, physicsSystem, navSystem) = CreatePhysicsWorld();

        // Fine-grained nav mesh
        var worldEntity = state.CreateEntity();
        var world = NavigationWorld2D.Default;
        world.BoundsMin = new Vector2(0, 0);
        world.BoundsMax = new Vector2(60, 60);
        world.CellSize = (Float)0.5f;
        world.AgentRadius = (Float)0.5f;
        world.ChunkSize = (Float)0; // deterministic full bake
        state.AddComponent(worldEntity, world);

        // Wide horizontal wall at Y=30, spanning X=15..45
        var wallPos = new Vector2(30, 30);
        var wallSize = new Vector2(30, 1);
        var wallEntity = state.CreateEntity();
        state.AddComponent(wallEntity, new Transform2D(wallPos, 0, Vector2.One));
        state.AddComponent(wallEntity, new StaticBody2D());
        state.AddComponent(wallEntity, CollisionShape2D.CreateRectangle(wallSize));

        // Agent close to the wall on one side, target directly behind on the other side
        var agentEntity = state.CreateEntity();
        state.AddComponent(agentEntity, new Transform2D(new Vector2(30, 29), 0, Vector2.One));
        var agent = NavigationAgent2D.Default;
        agent.TargetPosition = new Vector2(30, 35);
        agent.IsNavigationFinished = false;
        agent.Radius = (Float)0.5f;
        agent.MaxSpeed = (Float)10;
        agent.PathDesiredDistance = (Float)1;
        agent.TargetDesiredDistance = (Float)2;
        state.AddComponent(agentEntity, agent);

        // Bake nav mesh + compute path
        RunTick(state, transformSystem, physicsSystem, navSystem);

        var navState = state.GetCustomData<NavigationState>();
        navState!.HasPhysicsBakedMesh.Should().BeTrue();
        navState.AgentPaths.TryGetValue(agentEntity.Id, out var pathData).Should().BeTrue();
        pathData!.PathPoints.Count.Should().BeGreaterThan(1, "path should route around the wall");

        // Wall collision body AABB
        var wallMin = new Vector2(wallPos.X - wallSize.X / (Float)2, wallPos.Y - wallSize.Y / (Float)2);
        var wallMax = new Vector2(wallPos.X + wallSize.X / (Float)2, wallPos.Y + wallSize.Y / (Float)2);

        _output.WriteLine($"Wall body: ({(float)wallMin.X},{(float)wallMin.Y}) to ({(float)wallMax.X},{(float)wallMax.Y})");
        _output.WriteLine($"Path ({pathData.PathPoints.Count} waypoints):");
        for (int i = 0; i < pathData.PathPoints.Count; i++)
        {
            var wp = pathData.PathPoints[i];
            _output.WriteLine($"  [{i}] ({(float)wp.X:F2}, {(float)wp.Y:F2})");
        }

        // No path segment should cross the wall body
        for (int i = 0; i < pathData.PathPoints.Count - 1; i++)
        {
            var from = pathData.PathPoints[i];
            var to = pathData.PathPoints[i + 1];
            bool cuts = SegmentIntersectsAABB(from, to, wallMin, wallMax);
            cuts.Should().BeFalse(
                $"path segment [{i}]->({(float)from.X:F1},{(float)from.Y:F1})->({(float)to.X:F1},{(float)to.Y:F1}) " +
                $"should not cross wall body");
        }
    }

    [Fact]
    public void NavMesh_AfterObstacleAdded_NoTriangleEdgesCrossObstacle()
    {
        var (state, transformSystem, physicsSystem, navSystem) = CreatePhysicsWorld();

        // Nav world with chunking enabled (matches game setup)
        var worldEntity = state.CreateEntity();
        var world = NavigationWorld2D.Default;
        world.BoundsMin = new Vector2(-50, -50);
        world.BoundsMax = new Vector2(50, 50);
        world.CellSize = (Float)0.5f;
        world.AgentRadius = (Float)0.5f;
        world.ChunkSize = (Float)128; // chunked like the game
        state.AddComponent(worldEntity, world);

        // Step 1: Bake WITHOUT the obstacle
        RunTick(state, transformSystem, physicsSystem, navSystem);

        var navState = state.GetCustomData<NavigationState>();
        navState!.HasPhysicsBakedMesh.Should().BeTrue();
        int triCountBefore = navState.Map.Triangles.Count;
        _output.WriteLine($"Before obstacle: {triCountBefore} triangles");

        // Verify point at future obstacle location is ON the mesh
        var housePos = new Vector2(10, 10);
        navState.Map.FindTriangle(housePos).Should().BeGreaterOrEqualTo(0, "center should be on mesh before obstacle");

        // Step 2: Add a house-like obstacle (matches HouseDefinition: StaticBody2D + Rectangle 2x2)
        var houseEntity = state.CreateEntity();
        state.AddComponent(houseEntity, new Transform2D(housePos, 0, Vector2.One));
        state.AddComponent(houseEntity, new StaticBody2D()); // CollisionLayer=1
        state.AddComponent(houseEntity, CollisionShape2D.CreateRectangle(new Vector2(2, 2)));

        // Step 3: Let the nav mesh rebake incrementally (multiple ticks to ensure rebake)
        for (int i = 0; i < 3; i++)
            RunTick(state, transformSystem, physicsSystem, navSystem);

        int triCountAfter = navState.Map.Triangles.Count;
        _output.WriteLine($"After obstacle: {triCountAfter} triangles (invalidated: {navState.Map.InvalidatedCount})");

        // Obstacle center should now be OFF the mesh
        navState.Map.FindTriangle(housePos).Should().BeLessThan(0, "center should be carved out after obstacle");

        // Step 4: Check that NO triangle has edges crossing through the obstacle body
        // House collision: (9, 9) to (11, 11)
        var obstacleMin = new Vector2(housePos.X - 1, housePos.Y - 1);
        var obstacleMax = new Vector2(housePos.X + 1, housePos.Y + 1);

        int violatingTriangles = 0;
        for (int i = 0; i < navState.Map.Triangles.Count; i++)
        {
            var tri = navState.Map.Triangles[i];
            if (tri.V0 < 0) continue; // skip tombstoned

            var v0 = navState.Map.Vertices[tri.V0].Position;
            var v1 = navState.Map.Vertices[tri.V1].Position;
            var v2 = navState.Map.Vertices[tri.V2].Position;

            // Check if any edge of this triangle intersects the obstacle AABB
            bool e01 = SegmentIntersectsAABB(v0, v1, obstacleMin, obstacleMax);
            bool e12 = SegmentIntersectsAABB(v1, v2, obstacleMin, obstacleMax);
            bool e20 = SegmentIntersectsAABB(v2, v0, obstacleMin, obstacleMax);

            if (e01 || e12 || e20)
            {
                violatingTriangles++;
                if (violatingTriangles <= 5)
                {
                    _output.WriteLine($"Triangle [{i}] has edge through obstacle: " +
                        $"v0=({(float)v0.X:F1},{(float)v0.Y:F1}) v1=({(float)v1.X:F1},{(float)v1.Y:F1}) v2=({(float)v2.X:F1},{(float)v2.Y:F1}) " +
                        $"edges: 0-1={e01} 1-2={e12} 2-0={e20}");
                }
            }
        }

        _output.WriteLine($"Triangles with edges through obstacle: {violatingTriangles}");
        violatingTriangles.Should().Be(0, "no triangle edge should cross through the obstacle body after rebake");
    }

    [Fact]
    public void AgentNearWall_TargetStraightBehind_ReachesTargetWithPhysics()
    {
        var (state, transformSystem, physicsSystem, navSystem) = CreatePhysicsWorld();

        var worldEntity = state.CreateEntity();
        var world = NavigationWorld2D.Default;
        world.BoundsMin = new Vector2(0, 0);
        world.BoundsMax = new Vector2(60, 60);
        world.CellSize = (Float)0.5f;
        world.AgentRadius = (Float)0.5f;
        world.ChunkSize = (Float)0;
        state.AddComponent(worldEntity, world);

        var wallPos = new Vector2(30, 30);
        var wallSize = new Vector2(30, 1);
        var wallEntity = state.CreateEntity();
        state.AddComponent(wallEntity, new Transform2D(wallPos, 0, Vector2.One));
        state.AddComponent(wallEntity, new StaticBody2D());
        state.AddComponent(wallEntity, CollisionShape2D.CreateRectangle(wallSize));

        // CharacterBody2D agent near the wall (1 unit away, not touching)
        var agentEntity = state.CreateEntity();
        state.AddComponent(agentEntity, new Transform2D(new Vector2(30, 28), 0, Vector2.One));

        var body = CharacterBody2D.Default;
        body.CollisionLayer = 1;
        body.CollisionMask = uint.MaxValue;
        state.AddComponent(agentEntity, body);
        state.AddComponent(agentEntity, CollisionShape2D.CreateCircle((Float)0.5f));

        var agent = NavigationAgent2D.Default;
        agent.TargetPosition = new Vector2(30, 35);
        agent.IsNavigationFinished = false;
        agent.MaxSpeed = (Float)10;
        agent.PathDesiredDistance = (Float)1;
        agent.TargetDesiredDistance = (Float)2;
        agent.Radius = (Float)0.5f;
        agent.AvoidanceMask = 0;
        state.AddComponent(agentEntity, agent);

        // Bake
        for (int i = 0; i < 5; i++)
            RunTick(state, transformSystem, physicsSystem, navSystem);

        var startPos = state.GetComponent<Transform2D>(agentEntity).Position;
        var target = new Vector2(30, 35);

        // Simulate with physics
        int stuckTicks = 0;
        var prevPos = startPos;

        for (int tick = 0; tick < 300; tick++)
        {
            ref var nav = ref state.GetComponent<NavigationAgent2D>(agentEntity);
            ref var b = ref state.GetComponent<CharacterBody2D>(agentEntity);
            b.Velocity = nav.Velocity;

            RunTick(state, transformSystem, physicsSystem, navSystem);

            var pos = state.GetComponent<Transform2D>(agentEntity).Position;
            nav = ref state.GetComponent<NavigationAgent2D>(agentEntity);

            var moved = Vector2.Distance(pos, prevPos);
            bool navActive = nav.Velocity.SqrMagnitude > (Float)0.01f && !nav.IsNavigationFinished;

            if (navActive && (float)moved < 0.001f)
                stuckTicks++;
            else
                stuckTicks = 0;

            if (tick % 30 == 0)
            {
                _output.WriteLine($"T{tick}: ({(float)pos.X:F1},{(float)pos.Y:F1}) " +
                    $"vel=({(float)nav.Velocity.X:F1},{(float)nav.Velocity.Y:F1}) " +
                    $"dist={Vector2.Distance(pos, target):F1} stuck={stuckTicks}");
            }

            prevPos = pos;

            stuckTicks.Should().BeLessThan(30,
                $"Agent should not get stuck near wall " +
                $"(at ({(float)pos.X:F1},{(float)pos.Y:F1}) vel=({(float)nav.Velocity.X:F1},{(float)nav.Velocity.Y:F1}))");

            if (nav.IsNavigationFinished) break;
        }

        var endPos = state.GetComponent<Transform2D>(agentEntity).Position;
        var finalDist = (float)Vector2.Distance(endPos, target);
        _output.WriteLine($"\nFinal: ({(float)endPos.X:F1},{(float)endPos.Y:F1}) dist={finalDist:F1}");

        finalDist.Should().BeLessThan(5f, "Agent should reach the target by navigating around the wall");
    }
}

/// <summary>
/// Tests that an agent following a moving target in a straight line doesn't reverse direction.
/// Reproduces a bug where path recomputation at chunk boundaries or rectangle boundaries
/// causes the agent to briefly go backwards before correcting.
/// </summary>
[Collection("Sequential")]
public class StraightLineFollowTests
{
    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public StraightLineFollowTests(Xunit.Abstractions.ITestOutputHelper output) => _output = output;

    private (EntityWorld state, TransformSystem transformSystem, RapierPhysicsSystem physicsSystem, NavigationSystem navSystem) CreatePhysicsWorld()
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

        return (state, new TransformSystem(), new RapierPhysicsSystem(), new NavigationSystem());
    }

    private void RunTick(EntityWorld state, TransformSystem transformSystem, RapierPhysicsSystem physicsSystem, NavigationSystem navSystem)
    {
        var gameTime = state.GetCustomData<IGameTime>() as FakeGameTime;
        gameTime!.CurrentTick++;
        transformSystem.Update(state);
        physicsSystem.Update(state);
        navSystem.Update(state);
    }

    [Theory]
    [InlineData(0)]    // no chunking
    [InlineData(16)]   // small chunks (agent will cross boundaries)
    [InlineData(128)]  // large chunks (game default)
    public void Agent_FollowingStraightTarget_ShouldNotReverseDirection(float chunkSize)
    {
        var (state, transformSystem, physicsSystem, navSystem) = CreatePhysicsWorld();

        var worldEntity = state.CreateEntity();
        var world = NavigationWorld2D.Default;
        world.BoundsMin = new Vector2(-10, -10);
        world.BoundsMax = new Vector2(200, 50);
        world.CellSize = (Float)0.5f;
        world.AgentRadius = (Float)0.5f;
        world.ChunkSize = (Float)chunkSize;
        state.AddComponent(worldEntity, world);

        // Agent with CharacterBody2D
        var agentEntity = state.CreateEntity();
        state.AddComponent(agentEntity, new Transform2D(new Vector2(10, 20), 0, Vector2.One));
        var body = CharacterBody2D.Default;
        body.CollisionLayer = 1;
        body.CollisionMask = uint.MaxValue;
        state.AddComponent(agentEntity, body);
        state.AddComponent(agentEntity, CollisionShape2D.CreateCircle((Float)0.5f));

        var agent = NavigationAgent2D.Default;
        agent.TargetPosition = new Vector2(15, 20); // initial target ahead
        agent.IsNavigationFinished = false;
        agent.MaxSpeed = (Float)10;
        agent.PathDesiredDistance = (Float)1;
        agent.TargetDesiredDistance = (Float)2;
        agent.Radius = (Float)0.5f;
        agent.AvoidanceMask = 0;
        state.AddComponent(agentEntity, agent);

        // Bake
        for (int i = 0; i < 3; i++)
            RunTick(state, transformSystem, physicsSystem, navSystem);

        // Simulate: move target steadily to the right (like a player walking right)
        Float targetX = (Float)15;
        Float targetSpeed = (Float)8; // slightly slower than agent so agent can keep up
        Float dt = (Float)1 / (Float)60;
        Float targetUpdateThresholdSq = (Float)(1.5f * 1.5f); // match CowFollowSystem

        int reversals = 0;
        Float lastAgentX = (Float)10;
        Vector2 lastTarget = new Vector2(15, 20);

        _output.WriteLine($"ChunkSize={chunkSize}");

        for (int tick = 0; tick < 600; tick++)
        {
            // Move target to the right
            targetX += targetSpeed * dt;
            var targetPos = new Vector2(targetX, (Float)20);

            // Only update nav target when target moved significantly (like CowFollowSystem)
            ref var nav = ref state.GetComponent<NavigationAgent2D>(agentEntity);
            var targetDrift = (targetPos - lastTarget).SqrMagnitude;
            if (targetDrift > targetUpdateThresholdSq || nav.IsNavigationFinished)
            {
                nav.TargetPosition = targetPos;
                lastTarget = targetPos;
            }

            if ((targetPos - state.GetComponent<Transform2D>(agentEntity).Position).SqrMagnitude >
                nav.TargetDesiredDistance * nav.TargetDesiredDistance)
            {
                nav.IsNavigationFinished = false;
            }

            // Apply nav velocity to body
            ref var b = ref state.GetComponent<CharacterBody2D>(agentEntity);
            b.Velocity = nav.Velocity;

            RunTick(state, transformSystem, physicsSystem, navSystem);

            var agentPos = state.GetComponent<Transform2D>(agentEntity).Position;
            nav = ref state.GetComponent<NavigationAgent2D>(agentEntity);

            // Check for direction reversal: agent X decreased while target is ahead
            bool targetIsAhead = targetX > agentPos.X;
            bool agentWentBack = agentPos.X < lastAgentX - (Float)0.05f; // small tolerance

            if (targetIsAhead && agentWentBack && tick > 10) // skip first few ticks
            {
                reversals++;
                _output.WriteLine($"T{tick}: REVERSAL at ({(float)agentPos.X:F2},{(float)agentPos.Y:F2}) " +
                    $"vel=({(float)nav.Velocity.X:F1},{(float)nav.Velocity.Y:F1}) " +
                    $"target=({(float)targetX:F1},20) lastX={(float)lastAgentX:F2}");
            }

            if (tick % 60 == 0)
            {
                _output.WriteLine($"T{tick}: agent=({(float)agentPos.X:F1},{(float)agentPos.Y:F1}) " +
                    $"target=({(float)targetX:F1},20) " +
                    $"vel=({(float)nav.Velocity.X:F1},{(float)nav.Velocity.Y:F1}) " +
                    $"dist={(float)Vector2.Distance(agentPos, targetPos):F1}");
            }

            lastAgentX = agentPos.X;
        }

        _output.WriteLine($"\nTotal reversals: {reversals}");
        reversals.Should().Be(0, $"agent following a straight-line target should never reverse (chunkSize={chunkSize})");
    }
    [Theory]
    [InlineData(0)]
    [InlineData(16)]
    [InlineData(128)]
    public void Agent_FollowingStraightTarget_WithObstaclesNearby_ShouldNotReverseDirection(float chunkSize)
    {
        var (state, transformSystem, physicsSystem, navSystem) = CreatePhysicsWorld();

        var worldEntity = state.CreateEntity();
        var world = NavigationWorld2D.Default;
        world.BoundsMin = new Vector2(-10, -10);
        world.BoundsMax = new Vector2(200, 50);
        world.CellSize = (Float)0.5f;
        world.AgentRadius = (Float)0.5f;
        world.ChunkSize = (Float)chunkSize;
        state.AddComponent(worldEntity, world);

        // Place house-like obstacles (2x2) along both sides of the path at Y=20
        // This creates a corridor that the agent walks through
        for (float x = 0; x < 180; x += 8)
        {
            // Houses above the path
            var wallAbove = state.CreateEntity();
            state.AddComponent(wallAbove, new Transform2D(new Vector2((Float)x, (Float)16), 0, Vector2.One));
            state.AddComponent(wallAbove, new StaticBody2D());
            state.AddComponent(wallAbove, CollisionShape2D.CreateRectangle(new Vector2(2, 2)));

            // Houses below the path
            var wallBelow = state.CreateEntity();
            state.AddComponent(wallBelow, new Transform2D(new Vector2((Float)x, (Float)24), 0, Vector2.One));
            state.AddComponent(wallBelow, new StaticBody2D());
            state.AddComponent(wallBelow, CollisionShape2D.CreateRectangle(new Vector2(2, 2)));
        }

        // Agent
        var agentEntity = state.CreateEntity();
        state.AddComponent(agentEntity, new Transform2D(new Vector2(5, 20), 0, Vector2.One));
        var bodyComp = CharacterBody2D.Default;
        bodyComp.CollisionLayer = 1;
        bodyComp.CollisionMask = uint.MaxValue;
        state.AddComponent(agentEntity, bodyComp);
        state.AddComponent(agentEntity, CollisionShape2D.CreateCircle((Float)0.5f));

        var agent = NavigationAgent2D.Default;
        agent.TargetPosition = new Vector2(10, 20);
        agent.IsNavigationFinished = false;
        agent.MaxSpeed = (Float)10;
        agent.PathDesiredDistance = (Float)1;
        agent.TargetDesiredDistance = (Float)2;
        agent.Radius = (Float)0.5f;
        agent.AvoidanceMask = 0;
        state.AddComponent(agentEntity, agent);

        // Bake
        for (int i = 0; i < 5; i++)
            RunTick(state, transformSystem, physicsSystem, navSystem);

        Float targetX = (Float)10;
        Float targetSpeed = (Float)8;
        Float dt = (Float)1 / (Float)60;
        Float targetUpdateThresholdSq = (Float)(1.5f * 1.5f);

        int reversals = 0;
        Float lastAgentX = (Float)5;
        Vector2 lastTarget = new Vector2(10, 20);

        _output.WriteLine($"ChunkSize={chunkSize} with corridor obstacles");

        for (int tick = 0; tick < 600; tick++)
        {
            targetX += targetSpeed * dt;
            var targetPos = new Vector2(targetX, (Float)20);

            ref var nav = ref state.GetComponent<NavigationAgent2D>(agentEntity);
            var targetDrift = (targetPos - lastTarget).SqrMagnitude;
            if (targetDrift > targetUpdateThresholdSq || nav.IsNavigationFinished)
            {
                nav.TargetPosition = targetPos;
                lastTarget = targetPos;
            }

            if ((targetPos - state.GetComponent<Transform2D>(agentEntity).Position).SqrMagnitude >
                nav.TargetDesiredDistance * nav.TargetDesiredDistance)
            {
                nav.IsNavigationFinished = false;
            }

            ref var b = ref state.GetComponent<CharacterBody2D>(agentEntity);
            b.Velocity = nav.Velocity;

            RunTick(state, transformSystem, physicsSystem, navSystem);

            var agentPos = state.GetComponent<Transform2D>(agentEntity).Position;
            nav = ref state.GetComponent<NavigationAgent2D>(agentEntity);

            bool targetIsAhead = targetX > agentPos.X;
            bool agentWentBack = agentPos.X < lastAgentX - (Float)0.05f;

            if (targetIsAhead && agentWentBack && tick > 10)
            {
                reversals++;
                if (reversals <= 10)
                {
                    _output.WriteLine($"T{tick}: REVERSAL at ({(float)agentPos.X:F2},{(float)agentPos.Y:F2}) " +
                        $"vel=({(float)nav.Velocity.X:F1},{(float)nav.Velocity.Y:F1}) " +
                        $"target=({(float)targetX:F1},20)");
                }
            }

            if (tick % 60 == 0)
            {
                _output.WriteLine($"T{tick}: agent=({(float)agentPos.X:F1},{(float)agentPos.Y:F1}) " +
                    $"target=({(float)targetX:F1},20) " +
                    $"vel=({(float)nav.Velocity.X:F1},{(float)nav.Velocity.Y:F1}) " +
                    $"dist={(float)Vector2.Distance(agentPos, targetPos):F1}");
            }

            lastAgentX = agentPos.X;
        }

        _output.WriteLine($"\nTotal reversals: {reversals}");
        reversals.Should().Be(0, $"agent in corridor should not reverse (chunkSize={chunkSize})");
    }
    [Theory]
    [InlineData(0)]
    [InlineData(16)]
    [InlineData(128)]
    public void Agent_FollowingStraightTarget_StaggeredObstacles_ShouldNotReverseDirection(float chunkSize)
    {
        var (state, transformSystem, physicsSystem, navSystem) = CreatePhysicsWorld();

        var worldEntity = state.CreateEntity();
        var world = NavigationWorld2D.Default;
        world.BoundsMin = new Vector2(-10, -10);
        world.BoundsMax = new Vector2(200, 50);
        world.CellSize = (Float)0.5f;
        world.AgentRadius = (Float)0.5f;
        world.ChunkSize = (Float)chunkSize;
        state.AddComponent(worldEntity, world);

        // Houses (2x2) staggered along the path — matches game-like spacing
        bool above = true;
        for (float x = 5; x < 180; x += 10)
        {
            var obstacleY = above ? 17.0f : 23.0f; // 3 units from center path
            var obstacle = state.CreateEntity();
            state.AddComponent(obstacle, new Transform2D(new Vector2((Float)x, (Float)obstacleY), 0, Vector2.One));
            state.AddComponent(obstacle, new StaticBody2D());
            state.AddComponent(obstacle, CollisionShape2D.CreateRectangle(new Vector2(2, 2)));
            above = !above;
        }

        var agentEntity = state.CreateEntity();
        state.AddComponent(agentEntity, new Transform2D(new Vector2(0, 20), 0, Vector2.One));
        var bodyComp = CharacterBody2D.Default;
        bodyComp.CollisionLayer = 1;
        bodyComp.CollisionMask = uint.MaxValue;
        state.AddComponent(agentEntity, bodyComp);
        state.AddComponent(agentEntity, CollisionShape2D.CreateCircle((Float)0.5f));

        var agent = NavigationAgent2D.Default;
        agent.TargetPosition = new Vector2(5, 20);
        agent.IsNavigationFinished = false;
        agent.MaxSpeed = (Float)10;
        agent.PathDesiredDistance = (Float)1;
        agent.TargetDesiredDistance = (Float)2;
        agent.Radius = (Float)0.5f;
        agent.AvoidanceMask = 0;
        state.AddComponent(agentEntity, agent);

        for (int i = 0; i < 5; i++)
            RunTick(state, transformSystem, physicsSystem, navSystem);

        Float targetX = (Float)5;
        Float targetSpeed = (Float)8;
        Float dt = (Float)1 / (Float)60;
        Float targetUpdateThresholdSq = (Float)(1.5f * 1.5f);

        int reversals = 0;
        Float lastAgentX = (Float)0;
        Vector2 lastTarget = new Vector2(5, 20);

        _output.WriteLine($"ChunkSize={chunkSize} with staggered obstacles");

        for (int tick = 0; tick < 600; tick++)
        {
            targetX += targetSpeed * dt;
            var targetPos = new Vector2(targetX, (Float)20);

            ref var nav = ref state.GetComponent<NavigationAgent2D>(agentEntity);
            var targetDrift = (targetPos - lastTarget).SqrMagnitude;
            if (targetDrift > targetUpdateThresholdSq || nav.IsNavigationFinished)
            {
                nav.TargetPosition = targetPos;
                lastTarget = targetPos;
            }

            if ((targetPos - state.GetComponent<Transform2D>(agentEntity).Position).SqrMagnitude >
                nav.TargetDesiredDistance * nav.TargetDesiredDistance)
                nav.IsNavigationFinished = false;

            ref var b = ref state.GetComponent<CharacterBody2D>(agentEntity);
            b.Velocity = nav.Velocity;

            RunTick(state, transformSystem, physicsSystem, navSystem);

            var agentPos = state.GetComponent<Transform2D>(agentEntity).Position;
            nav = ref state.GetComponent<NavigationAgent2D>(agentEntity);

            // Check for nav velocity reversal: velocity has significant backwards component
            // along the direction to the target (not just X axis)
            var toTgt = targetPos - agentPos;
            var toTgtMag = toTgt.Magnitude;
            if ((float)toTgtMag > 2f && tick > 10 && nav.Velocity.SqrMagnitude > (Float)0.1f)
            {
                var toTgtNorm = toTgt / toTgtMag;
                var fwdComponent = nav.Velocity.X * toTgtNorm.X + nav.Velocity.Y * toTgtNorm.Y;
                // Reversal = velocity has significant backwards component toward target
                if ((float)fwdComponent < -2f)
                {
                    reversals++;
                    if (reversals <= 10)
                        _output.WriteLine($"T{tick}: REVERSAL at ({(float)agentPos.X:F2},{(float)agentPos.Y:F2}) " +
                            $"vel=({(float)nav.Velocity.X:F1},{(float)nav.Velocity.Y:F1}) " +
                            $"fwd={fwdComponent:F1} target=({(float)targetX:F1},20)");
                }
            }

            if (tick % 60 == 0)
                _output.WriteLine($"T{tick}: agent=({(float)agentPos.X:F1},{(float)agentPos.Y:F1}) " +
                    $"target=({(float)targetX:F1},20) vel=({(float)nav.Velocity.X:F1},{(float)nav.Velocity.Y:F1}) " +
                    $"dist={(float)Vector2.Distance(agentPos, targetPos):F1}");

            lastAgentX = agentPos.X;
        }

        _output.WriteLine($"\nTotal reversals: {reversals}");
        // In tight zigzag corridors, brief reversals during path recomputation
        // are expected. The velocity anti-backtrack catches most cases when LOS exists.
        // Limit to at most 5% of ticks having reversals (30 out of 600).
        reversals.Should().BeLessThan(30, $"agent should not frequently reverse in corridor (chunkSize={chunkSize})");
    }
}

/// <summary>
/// Tests that navigation produces identical results across independent simulations
/// and after state serialization/deserialization (simulating client-server sync).
/// </summary>
[Collection("Sequential")]
public class NavigationDeterminismTests
{
    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public NavigationDeterminismTests(Xunit.Abstractions.ITestOutputHelper output) => _output = output;

    private (EntityWorld state, TransformSystem transformSystem, RapierPhysicsSystem physicsSystem, NavigationSystem navSystem) CreateWorld()
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

        state.SetCustomData<IGameTime>(new FakeGameTime { CurrentTick = 0 });

        return (state, new TransformSystem(), new RapierPhysicsSystem(), new NavigationSystem());
    }

    private void RunTick(EntityWorld state, TransformSystem ts, RapierPhysicsSystem ps, NavigationSystem ns)
    {
        var gt = state.GetCustomData<IGameTime>() as FakeGameTime;
        gt!.CurrentTick++;
        ts.Update(state);
        ps.Update(state);
        ns.Update(state);
    }

    private void SetupScene(EntityWorld state)
    {
        // Nav world
        var nw = state.CreateEntity();
        var world = NavigationWorld2D.Default;
        world.BoundsMin = new Vector2(-10, -10);
        world.BoundsMax = new Vector2(100, 50);
        world.CellSize = (Float)0.5f;
        world.AgentRadius = (Float)0.5f;
        world.ChunkSize = (Float)128;
        state.AddComponent(nw, world);

        // Some obstacles
        for (int i = 0; i < 5; i++)
        {
            var wall = state.CreateEntity();
            state.AddComponent(wall, new Transform2D(new Vector2((Float)(10 + i * 15), (Float)20), 0, Vector2.One));
            state.AddComponent(wall, new StaticBody2D());
            state.AddComponent(wall, CollisionShape2D.CreateRectangle(new Vector2(2, 2)));
        }

        // Agent with CharacterBody2D navigating through the scene
        var agent = state.CreateEntity();
        state.AddComponent(agent, new Transform2D(new Vector2(5, 20), 0, Vector2.One));
        var body = CharacterBody2D.Default;
        body.CollisionLayer = 1;
        body.CollisionMask = uint.MaxValue;
        state.AddComponent(agent, body);
        state.AddComponent(agent, CollisionShape2D.CreateCircle((Float)0.5f));

        var nav = NavigationAgent2D.Default;
        nav.TargetPosition = new Vector2(80, 20);
        nav.IsNavigationFinished = false;
        nav.MaxSpeed = (Float)10;
        nav.PathDesiredDistance = (Float)1;
        nav.TargetDesiredDistance = (Float)2;
        nav.Radius = (Float)0.5f;
        nav.AvoidanceMask = 0;
        state.AddComponent(agent, nav);
    }

    [Fact]
    public void TwoIdenticalSimulations_ProduceIdenticalState()
    {
        // Run simulation A
        var (stateA, tsA, psA, nsA) = CreateWorld();
        SetupScene(stateA);
        for (int i = 0; i < 120; i++)
        {
            // Apply nav velocity to body (like CowFollowSystem)
            foreach (var e in stateA.Filter<NavigationAgent2D, CharacterBody2D>())
            {
                ref var nav = ref stateA.GetComponent<NavigationAgent2D>(e);
                ref var b = ref stateA.GetComponent<CharacterBody2D>(e);
                b.Velocity = nav.Velocity;
            }
            RunTick(stateA, tsA, psA, nsA);
        }

        // Run simulation B (independently)
        var (stateB, tsB, psB, nsB) = CreateWorld();
        SetupScene(stateB);
        for (int i = 0; i < 120; i++)
        {
            foreach (var e in stateB.Filter<NavigationAgent2D, CharacterBody2D>())
            {
                ref var nav = ref stateB.GetComponent<NavigationAgent2D>(e);
                ref var b = ref stateB.GetComponent<CharacterBody2D>(e);
                b.Velocity = nav.Velocity;
            }
            RunTick(stateB, tsB, psB, nsB);
        }

        var hashA = StateHasher.Hash(stateA);
        var hashB = StateHasher.Hash(stateB);

        _output.WriteLine($"Hash A: {hashA}");
        _output.WriteLine($"Hash B: {hashB}");

        hashA.Should().Be(hashB, "two identical simulations must produce identical state");
    }

    [Fact]
    public void TwoSimulations_FromSameSnapshot_ProduceIdenticalState()
    {
        // Run a simulation for 60 ticks to create interesting state
        var (source, tsS, psS, nsS) = CreateWorld();
        SetupScene(source);
        for (int i = 0; i < 60; i++)
        {
            foreach (var e in source.Filter<NavigationAgent2D, CharacterBody2D>())
            {
                ref var nav = ref source.GetComponent<NavigationAgent2D>(e);
                ref var b = ref source.GetComponent<CharacterBody2D>(e);
                b.Velocity = nav.Velocity;
            }
            RunTick(source, tsS, psS, nsS);
        }

        // Serialize at tick 60
        byte[] snapshot = StateSerializer.Serialize(source);
        _output.WriteLine($"Snapshot at tick 60: {StateHasher.Hash(source)}");

        // Create two independent simulations from the same snapshot
        var (simA, tsA, psA, nsA) = CreateWorld();
        SetupScene(simA);
        StateSerializer.Deserialize(simA, snapshot, syncComponentIds: false);

        var (simB, tsB, psB, nsB) = CreateWorld();
        SetupScene(simB);
        StateSerializer.Deserialize(simB, snapshot, syncComponentIds: false);

        // Run both for 60 ticks
        for (int i = 0; i < 60; i++)
        {
            foreach (var e in simA.Filter<NavigationAgent2D, CharacterBody2D>())
            {
                ref var nav = ref simA.GetComponent<NavigationAgent2D>(e);
                ref var b = ref simA.GetComponent<CharacterBody2D>(e);
                b.Velocity = nav.Velocity;
            }
            RunTick(simA, tsA, psA, nsA);

            foreach (var e in simB.Filter<NavigationAgent2D, CharacterBody2D>())
            {
                ref var nav = ref simB.GetComponent<NavigationAgent2D>(e);
                ref var b = ref simB.GetComponent<CharacterBody2D>(e);
                b.Velocity = nav.Velocity;
            }
            RunTick(simB, tsB, psB, nsB);
        }

        var hashA = StateHasher.Hash(simA);
        var hashB = StateHasher.Hash(simB);

        _output.WriteLine($"Sim A hash: {hashA}");
        _output.WriteLine($"Sim B hash: {hashB}");

        hashA.Should().Be(hashB,
            "two simulations starting from the same snapshot must produce identical state — " +
            "this verifies that NavigationState is rebuilt deterministically from ECS data");
    }

    [Fact]
    public void TwoSimulations_FromSnapshotWithObstacle_ProduceIdenticalState()
    {
        // Run with obstacle added midway
        var (source, tsS, psS, nsS) = CreateWorld();
        SetupScene(source);
        for (int i = 0; i < 30; i++)
        {
            foreach (var e in source.Filter<NavigationAgent2D, CharacterBody2D>())
            {
                ref var nav = ref source.GetComponent<NavigationAgent2D>(e);
                ref var b = ref source.GetComponent<CharacterBody2D>(e);
                b.Velocity = nav.Velocity;
            }
            RunTick(source, tsS, psS, nsS);
        }

        // Add obstacle
        var house = source.CreateEntity();
        source.AddComponent(house, new Transform2D(new Vector2(40, 20), 0, Vector2.One));
        source.AddComponent(house, new StaticBody2D());
        source.AddComponent(house, CollisionShape2D.CreateRectangle(new Vector2(2, 2)));

        for (int i = 0; i < 30; i++)
        {
            foreach (var e in source.Filter<NavigationAgent2D, CharacterBody2D>())
            {
                ref var nav = ref source.GetComponent<NavigationAgent2D>(e);
                ref var b = ref source.GetComponent<CharacterBody2D>(e);
                b.Velocity = nav.Velocity;
            }
            RunTick(source, tsS, psS, nsS);
        }

        byte[] snapshot = StateSerializer.Serialize(source);

        var (simA, tsA, psA, nsA) = CreateWorld();
        SetupScene(simA);
        StateSerializer.Deserialize(simA, snapshot, syncComponentIds: false);

        var (simB, tsB, psB, nsB) = CreateWorld();
        SetupScene(simB);
        StateSerializer.Deserialize(simB, snapshot, syncComponentIds: false);

        for (int i = 0; i < 60; i++)
        {
            foreach (var e in simA.Filter<NavigationAgent2D, CharacterBody2D>())
            {
                ref var nav = ref simA.GetComponent<NavigationAgent2D>(e);
                ref var b = ref simA.GetComponent<CharacterBody2D>(e);
                b.Velocity = nav.Velocity;
            }
            RunTick(simA, tsA, psA, nsA);

            foreach (var e in simB.Filter<NavigationAgent2D, CharacterBody2D>())
            {
                ref var nav = ref simB.GetComponent<NavigationAgent2D>(e);
                ref var b = ref simB.GetComponent<CharacterBody2D>(e);
                b.Velocity = nav.Velocity;
            }
            RunTick(simB, tsB, psB, nsB);
        }

        var hashA = StateHasher.Hash(simA);
        var hashB = StateHasher.Hash(simB);

        _output.WriteLine($"Sim A hash: {hashA}");
        _output.WriteLine($"Sim B hash: {hashB}");

        hashA.Should().Be(hashB,
            "two simulations from same snapshot with obstacle must produce identical state");
    }
}

internal class FakeGameTime : IGameTime
{
    public long CurrentTick { get; set; }
    public Float FixedDeltaTime { get; set; } = (Float)1 / (Float)60;
    public int TickRate { get; set; } = 60;
    public bool IsResimulating { get; set; } = false;
}
