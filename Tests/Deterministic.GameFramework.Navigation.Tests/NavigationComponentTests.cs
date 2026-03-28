using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Navigation2D.Components;
using Deterministic.GameFramework.TwoD;
using Deterministic.GameFramework.Common;
using Deterministic.GameFramework.Types;
using FluentAssertions;
using Xunit;

namespace Deterministic.GameFramework.Navigation.Tests;

[Collection("Sequential")]
public class NavigationComponentTests
{
    public NavigationComponentTests()
    {
        ServiceLocator.Reset();
        ServiceLocator.RegisterAssembly(typeof(EntityWorld).Assembly);
        ServiceLocator.RegisterAssembly(typeof(Transform2D).Assembly);
        ServiceLocator.RegisterAssembly(typeof(NavigationRegion2D).Assembly);
    }

    [Fact]
    public void NavigationRegion2D_CanBeRegisteredAndAttached()
    {
        var state = new EntityWorld();
        state.RegisterComponent<Transform2D>();
        state.RegisterComponent<NavigationRegion2D>();

        var entity = state.CreateEntity();
        state.AddComponent(entity, Transform2D.Default);
        state.AddComponent(entity, NavigationRegion2D.Default);

        state.HasComponent<NavigationRegion2D>(entity).Should().BeTrue();
    }

    [Fact]
    public void NavigationAgent2D_CanBeRegisteredAndAttached()
    {
        var state = new EntityWorld();
        state.RegisterComponent<Transform2D>();
        state.RegisterComponent<NavigationAgent2D>();

        var entity = state.CreateEntity();
        state.AddComponent(entity, Transform2D.Default);
        state.AddComponent(entity, NavigationAgent2D.Default);

        state.HasComponent<NavigationAgent2D>(entity).Should().BeTrue();
    }

    [Fact]
    public void NavigationObstacle2D_CanBeRegisteredAndAttached()
    {
        var state = new EntityWorld();
        state.RegisterComponent<Transform2D>();
        state.RegisterComponent<NavigationObstacle2D>();

        var entity = state.CreateEntity();
        state.AddComponent(entity, Transform2D.Default);
        state.AddComponent(entity, NavigationObstacle2D.Default);

        state.HasComponent<NavigationObstacle2D>(entity).Should().BeTrue();
    }

    [Fact]
    public void NavigationRegion2D_Default_HasCorrectValues()
    {
        var region = NavigationRegion2D.Default;
        region.NavigationLayers.Should().Be(1);
        region.Enabled.Should().BeTrue();
        region.VertexCount.Should().Be(0);
        ((float)region.TravelCost).Should().BeApproximately(1f, 0.01f);
        ((float)region.EnterCost).Should().BeApproximately(0f, 0.01f);
    }

    [Fact]
    public void NavigationAgent2D_Default_HasCorrectValues()
    {
        var agent = NavigationAgent2D.Default;
        agent.NavigationLayers.Should().Be(1);
        agent.IsNavigationFinished.Should().BeTrue();
        agent.IsTargetReachable.Should().BeFalse();
        agent.AvoidanceEnabled.Should().BeFalse();
        ((float)agent.MaxSpeed).Should().BeApproximately(200f, 0.1f);
    }

    [Fact]
    public void FixedPolygon16_GetSet_Works()
    {
        var poly = new FixedPolygon16();
        poly[0] = new Vector2(1, 2);
        poly[5] = new Vector2(10, 20);
        poly[15] = new Vector2(100, 200);

        poly[0].Should().Be(new Vector2(1, 2));
        poly[5].Should().Be(new Vector2(10, 20));
        poly[15].Should().Be(new Vector2(100, 200));
    }

    [Fact]
    public void FixedPolygon16_Equality_Works()
    {
        var a = new FixedPolygon16();
        a[0] = new Vector2(1, 2);
        a[1] = new Vector2(3, 4);

        var b = new FixedPolygon16();
        b[0] = new Vector2(1, 2);
        b[1] = new Vector2(3, 4);

        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void FixedPolygon16_OutOfRange_Throws()
    {
        var poly = new FixedPolygon16();
        var act = () => { var _ = poly[16]; };
        act.Should().Throw<IndexOutOfRangeException>();
    }

    [Fact]
    public void NavigationRegion2D_CanBeFilteredWithTransform()
    {
        var state = new EntityWorld();
        state.RegisterComponent<Transform2D>();
        state.RegisterComponent<NavigationRegion2D>();

        var entity = state.CreateEntity();
        state.AddComponent(entity, Transform2D.Default);
        state.AddComponent(entity, NavigationRegion2D.Default);

        bool found = false;
        foreach (var e in state.Filter<NavigationRegion2D, Transform2D>())
        {
            if (e.Id == entity.Id) { found = true; break; }
        }
        found.Should().BeTrue();
    }
}
