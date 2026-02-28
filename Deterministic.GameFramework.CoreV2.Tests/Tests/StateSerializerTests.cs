using System;
using Deterministic.GameFramework.CoreV2;
using Deterministic.GameFramework.CoreV2.Example.Components;
using FluentAssertions;
using Xunit;

namespace Deterministic.GameFramework.CoreV2.Tests
{
    public class StateSerializerTests
    {
        [Fact]
        public void RoundTrip_ShouldPreserveCompleteState()
        {
            var state = new GlobalState();
            
            var entity1 = state.CreateEntity();
            var entity2 = state.CreateEntity();
            
            ref var health1 = ref state.GetComponent<HealthComponent>(entity1);
            health1.CurrentHealth = 100;
            
            ref var health2 = ref state.GetComponent<HealthComponent>(entity2);
            health2.CurrentHealth = 50;
            
            ref var region = ref state.GetComponent<RegionComponent>(entity1);
            region.DamageCounter = 25;
            
            byte[] serialized = StateSerializer.Serialize(state);
            
            var newState = new GlobalState();
            newState.RegisterComponent<HealthComponent>();
            newState.RegisterComponent<RegionComponent>();
            StateSerializer.Deserialize(newState, serialized);
            
            newState.GetComponent<HealthComponent>(entity1).CurrentHealth.Value.Should().Be(100);
            newState.GetComponent<HealthComponent>(entity2).CurrentHealth.Value.Should().Be(50);
            newState.GetComponent<RegionComponent>(entity1).DamageCounter.Should().Be(25);
        }

        [Fact]
        public void Serialize_ShouldBeDeterministic()
        {
            var state1 = new GlobalState();
            var state2 = new GlobalState();
            
            for (int i = 0; i < 10; i++)
            {
                var e1 = state1.CreateEntity();
                var e2 = state2.CreateEntity();
                
                ref var h1 = ref state1.GetComponent<HealthComponent>(e1);
                ref var h2 = ref state2.GetComponent<HealthComponent>(e2);
                
                h1.CurrentHealth = i * 10;
                h2.CurrentHealth = i * 10;
            }
            
            byte[] bytes1 = StateSerializer.Serialize(state1);
            byte[] bytes2 = StateSerializer.Serialize(state2);
            
            bytes1.Should().Equal(bytes2);
        }

        [Fact]
        public void Deserialize_ShouldThrowOnVersionMismatch()
        {
            byte[] corruptedData = new byte[] { 0xFF, 0xFF, 0, 0, 0, 0 };
            
            var state = new GlobalState();
            
            Action act = () => StateSerializer.Deserialize(state, corruptedData);
            act.Should().Throw<Exception>().WithMessage("*Version Mismatch*");
        }

        [Fact]
        public void RoundTrip_ShouldHandleLargeEntityCounts()
        {
            var state = new GlobalState();
            
            for (int i = 0; i < 500; i++)
            {
                var entity = state.CreateEntity();
                ref var health = ref state.GetComponent<HealthComponent>(entity);
                health.CurrentHealth = i;
            }
            
            byte[] serialized = StateSerializer.Serialize(state);
            
            var newState = new GlobalState();
            newState.RegisterComponent<HealthComponent>();
            StateSerializer.Deserialize(newState, serialized);
            
            for (int i = 0; i < 500; i++)
            {
                var entity = new Entity(i);
                newState.GetComponent<HealthComponent>(entity).CurrentHealth.Value.Should().Be(i);
            }
        }

        [Fact]
        public void RoundTrip_ShouldPreserveEntityMasks()
        {
            var state = new GlobalState();
            
            var entity1 = state.CreateEntity();
            var entity2 = state.CreateEntity();
            
            state.GetComponent<HealthComponent>(entity1);
            state.GetComponent<RegionComponent>(entity1);
            state.GetComponent<HealthComponent>(entity2);
            
            byte[] serialized = StateSerializer.Serialize(state);
            
            var newState = new GlobalState();
            newState.RegisterComponent<HealthComponent>();
            newState.RegisterComponent<RegionComponent>();
            StateSerializer.Deserialize(newState, serialized);
            
            newState.HasComponent<HealthComponent>(entity1).Should().BeTrue();
            newState.HasComponent<RegionComponent>(entity1).Should().BeTrue();
            newState.HasComponent<HealthComponent>(entity2).Should().BeTrue();
            newState.HasComponent<RegionComponent>(entity2).Should().BeFalse();
        }

        [Fact]
        public void Serialize_ShouldHandleEmptyState()
        {
            var state = new GlobalState();
            
            byte[] serialized = StateSerializer.Serialize(state);
            serialized.Should().NotBeNull();
            serialized.Length.Should().BeGreaterThan(0);
            
            var newState = new GlobalState();
            StateSerializer.Deserialize(newState, serialized);
            
            newState._nextEntityId.Should().Be(0);
        }

        [Fact]
        public void Deserialize_ShouldClearComponents_IfSourceDidNotHaveThem()
        {
            var state = new GlobalState();
            state.RegisterComponent<HealthComponent>();
            var entity = state.CreateEntity();
            
            // Snapshot 1: Empty
            byte[] emptyData = StateSerializer.Serialize(state);
            
            // Modify: Add Component
            state.AddComponent(entity, new HealthComponent { CurrentHealth = 100 });
            state.HasComponent<HealthComponent>(entity).Should().BeTrue();
            
            // Deserialize Snapshot 1
            StateSerializer.Deserialize(state, emptyData);
            
            // Verify: Component should be gone
            state.HasComponent<HealthComponent>(entity).Should().BeFalse();
        }
    }
}
