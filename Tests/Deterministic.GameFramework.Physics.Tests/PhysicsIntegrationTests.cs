using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.TwoD;
using Deterministic.GameFramework.Physics2D.Components;
using Deterministic.GameFramework.Common;
using FluentAssertions;
using Xunit;

namespace Deterministic.GameFramework.Physics.Tests;

[Collection("Sequential")]
public class PhysicsIntegrationTests
{
    public PhysicsIntegrationTests()
    {
        ServiceLocator.Reset();
        ServiceLocator.RegisterAssembly(typeof(EntityWorld).Assembly);
        ServiceLocator.RegisterAssembly(typeof(Transform2D).Assembly);
        ServiceLocator.RegisterAssembly(typeof(Area2D).Assembly);
    }

    [Fact]
    public void CanRegisterAndAttachPhysicsComponents_ToEntity()
    {
        var state = new EntityWorld();

        state.RegisterComponent<Transform2D>();
        state.RegisterComponent<RigidBody2D>();
        state.RegisterComponent<StaticBody2D>();
        state.RegisterComponent<CollisionShape2D>();
        state.RegisterComponent<PhysicsWorldState>();

        var entity = state.CreateEntity();

        state.AddComponent(entity, Transform2D.Default);
        state.AddComponent(entity, RigidBody2D.Default);
        state.AddComponent(entity, new StaticBody2D());
        state.AddComponent(entity, CollisionShape2D.CreateCircle(1.0f));
        state.AddComponent(entity, new PhysicsWorldState());

        state.HasComponent<Transform2D>(entity).Should().BeTrue();
        state.HasComponent<RigidBody2D>(entity).Should().BeTrue();
        state.HasComponent<StaticBody2D>(entity).Should().BeTrue();
        state.HasComponent<CollisionShape2D>(entity).Should().BeTrue();
        state.HasComponent<PhysicsWorldState>(entity).Should().BeTrue();
    }

    [Fact]
    public void PhysicsComponents_CanBeQueriedViaFilters()
    {
        var state = new EntityWorld();

        state.RegisterComponent<Transform2D>();
        state.RegisterComponent<RigidBody2D>();

        var entity = state.CreateEntity();
        state.AddComponent(entity, Transform2D.Default);
        state.AddComponent(entity, RigidBody2D.Default);

        var found = false;
        foreach (var e in state.Filter<Transform2D, RigidBody2D>())
        {
            if (e.Id == entity.Id)
            {
                found = true;
                break;
            }
        }

        found.Should().BeTrue("entities with Transform2D and RigidBody2D should be discoverable via ECS filters");
    }
}

