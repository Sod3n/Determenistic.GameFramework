using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Navigation2D.Components;
using Deterministic.GameFramework.Navigation2D.Systems;
using Deterministic.GameFramework.TwoD;
using Deterministic.GameFramework.Common;
using Deterministic.GameFramework.Types;
using FluentAssertions;
using Xunit;

namespace Deterministic.GameFramework.Navigation.Tests;

[Collection("Sequential")]
public class NavigationSystemTests
{
    private (EntityWorld state, NavigationSystem system, TransformSystem transformSystem) CreateWorld()
    {
        ServiceLocator.Reset();
        ServiceLocator.RegisterAssembly(typeof(EntityWorld).Assembly);
        ServiceLocator.RegisterAssembly(typeof(Transform2D).Assembly);
        ServiceLocator.RegisterAssembly(typeof(NavigationRegion2D).Assembly);

        var state = new EntityWorld();
        state.RegisterComponent<Transform2D>();
        state.RegisterComponent<NavigationRegion2D>();
        state.RegisterComponent<NavigationAgent2D>();
        state.RegisterComponent<NavigationObstacle2D>();

        var transformSystem = new TransformSystem();
        var navSystem = new NavigationSystem();

        return (state, navSystem, transformSystem);
    }

    private Entity CreateSquareRegion(EntityWorld state, Vector2 position, Float size)
    {
        var entity = state.CreateEntity();
        state.AddComponent(entity, new Transform2D(position, 0, Vector2.One));

        var halfSize = size / (Float)2;
        var region = NavigationRegion2D.Default;
        region.VertexCount = 4;
        region.Vertices[0] = new Vector2(-halfSize, -halfSize);
        region.Vertices[1] = new Vector2(halfSize, -halfSize);
        region.Vertices[2] = new Vector2(halfSize, halfSize);
        region.Vertices[3] = new Vector2(-halfSize, halfSize);
        state.AddComponent(entity, region);

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
    public void System_WithNoRegions_AgentStaysIdle()
    {
        var (state, navSystem, transformSystem) = CreateWorld();

        var agentEntity = CreateAgent(state, new Vector2(5, 5), new Vector2(50, 50));

        transformSystem.Update(state);
        navSystem.Update(state);

        ref var agent = ref state.GetComponent<NavigationAgent2D>(agentEntity);
        agent.Velocity.Should().Be(Vector2.Zero);
        agent.IsNavigationFinished.Should().BeTrue();
    }

    [Fact]
    public void System_AgentAtTarget_IsFinished()
    {
        var (state, navSystem, transformSystem) = CreateWorld();

        CreateSquareRegion(state, new Vector2(50, 50), (Float)100);
        var agentEntity = CreateAgent(state, new Vector2(50, 50), new Vector2(50, 50));

        transformSystem.Update(state);
        navSystem.Update(state);

        // First update computes path, second update detects we're already there
        navSystem.Update(state);

        ref var agent = ref state.GetComponent<NavigationAgent2D>(agentEntity);
        // Agent is already at the target (within TargetDesiredDistance)
        agent.IsNavigationFinished.Should().BeTrue();
    }

    [Fact]
    public void System_AgentFindsPath_ToTarget()
    {
        var (state, navSystem, transformSystem) = CreateWorld();

        CreateSquareRegion(state, new Vector2(50, 50), (Float)100);
        var agentEntity = CreateAgent(state, new Vector2(10, 10), new Vector2(90, 90));

        transformSystem.Update(state);
        navSystem.Update(state);

        ref var agent = ref state.GetComponent<NavigationAgent2D>(agentEntity);
        agent.IsTargetReachable.Should().BeTrue();
        agent.IsNavigationFinished.Should().BeFalse();
        agent.Velocity.Should().NotBe(Vector2.Zero, "agent should have a velocity toward the target");
    }

    [Fact]
    public void System_AgentVelocity_PointsTowardTarget()
    {
        var (state, navSystem, transformSystem) = CreateWorld();

        CreateSquareRegion(state, new Vector2(50, 50), (Float)100);
        var agentEntity = CreateAgent(state, new Vector2(10, 10), new Vector2(90, 90));

        transformSystem.Update(state);
        navSystem.Update(state);

        ref var agent = ref state.GetComponent<NavigationAgent2D>(agentEntity);

        // Velocity should generally point toward target (positive X and Y)
        ((float)agent.Velocity.X).Should().BeGreaterThan(0);
        ((float)agent.Velocity.Y).Should().BeGreaterThan(0);
    }

    [Fact]
    public void System_AgentVelocity_RespectsMaxSpeed()
    {
        var (state, navSystem, transformSystem) = CreateWorld();

        CreateSquareRegion(state, new Vector2(50, 50), (Float)100);
        var agentEntity = CreateAgent(state, new Vector2(10, 10), new Vector2(90, 90));

        transformSystem.Update(state);
        navSystem.Update(state);

        ref var agent = ref state.GetComponent<NavigationAgent2D>(agentEntity);
        var speed = (float)agent.Velocity.Magnitude;

        speed.Should().BeApproximately((float)agent.MaxSpeed, 1f);
    }

    [Fact]
    public void System_TargetChange_RecomputesPath()
    {
        var (state, navSystem, transformSystem) = CreateWorld();

        CreateSquareRegion(state, new Vector2(50, 50), (Float)100);
        var agentEntity = CreateAgent(state, new Vector2(10, 10), new Vector2(90, 90));

        transformSystem.Update(state);
        navSystem.Update(state);

        ref var agent = ref state.GetComponent<NavigationAgent2D>(agentEntity);
        var firstVelocity = agent.Velocity;

        // Change target to opposite direction
        agent.TargetPosition = new Vector2(10, 90);
        state.AddComponent(agentEntity, agent);

        navSystem.Update(state);

        ref var updatedAgent = ref state.GetComponent<NavigationAgent2D>(agentEntity);
        updatedAgent.IsTargetPositionChanged.Should().BeTrue();
    }

    [Fact]
    public void System_DisabledRegion_IsIgnored()
    {
        var (state, navSystem, transformSystem) = CreateWorld();

        var regionEntity = CreateSquareRegion(state, new Vector2(50, 50), (Float)100);

        // Disable the region
        ref var region = ref state.GetComponent<NavigationRegion2D>(regionEntity);
        region.Enabled = false;

        var agentEntity = CreateAgent(state, new Vector2(10, 10), new Vector2(90, 90));

        transformSystem.Update(state);
        navSystem.Update(state);

        ref var agent = ref state.GetComponent<NavigationAgent2D>(agentEntity);
        agent.IsTargetReachable.Should().BeFalse();
    }

    [Fact]
    public void System_MultipleRegions_PathAcross()
    {
        var (state, navSystem, transformSystem) = CreateWorld();

        // Two adjacent regions
        CreateSquareRegion(state, new Vector2(25, 50), (Float)50);
        CreateSquareRegion(state, new Vector2(75, 50), (Float)50);

        var agentEntity = CreateAgent(state, new Vector2(10, 50), new Vector2(90, 50));

        transformSystem.Update(state);
        navSystem.Update(state);

        ref var agent = ref state.GetComponent<NavigationAgent2D>(agentEntity);
        agent.IsTargetReachable.Should().BeTrue();
        agent.IsNavigationFinished.Should().BeFalse();
    }

    [Fact]
    public void System_AgentWithoutRegion_DoesNotCrash()
    {
        var (state, navSystem, transformSystem) = CreateWorld();

        // Just an agent, no regions
        CreateAgent(state, new Vector2(5, 5), new Vector2(50, 50));

        transformSystem.Update(state);

        // Should not throw
        var act = () => navSystem.Update(state);
        act.Should().NotThrow();
    }

    [Fact]
    public void System_RegionRemoved_MapRebuilds()
    {
        var (state, navSystem, transformSystem) = CreateWorld();

        var regionEntity = CreateSquareRegion(state, new Vector2(50, 50), (Float)100);
        var agentEntity = CreateAgent(state, new Vector2(10, 10), new Vector2(90, 90));

        transformSystem.Update(state);
        navSystem.Update(state);

        ref var agent1 = ref state.GetComponent<NavigationAgent2D>(agentEntity);
        agent1.IsTargetReachable.Should().BeTrue();

        // Remove region
        state.DeleteEntity(regionEntity);

        // Agent target changed so re-trigger
        ref var agent2 = ref state.GetComponent<NavigationAgent2D>(agentEntity);
        agent2.TargetPosition = new Vector2(90, 91); // slightly different to trigger recompute

        transformSystem.Update(state);
        navSystem.Update(state);

        ref var agent3 = ref state.GetComponent<NavigationAgent2D>(agentEntity);
        agent3.IsTargetReachable.Should().BeFalse();
    }
}
