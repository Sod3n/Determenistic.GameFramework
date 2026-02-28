using System.Linq;
using Deterministic.GameFramework.CoreV2;
using Deterministic.GameFramework.CoreV2.Example.Components;
using FluentAssertions;
using Xunit;

namespace Deterministic.GameFramework.CoreV2.Tests
{
    public class GlobalStateTests
    {
        [Fact]
        public void CreateEntity_ShouldGenerateUniqueIds()
        {
            var state = new GlobalState();
            
            var e1 = state.CreateEntity();
            var e2 = state.CreateEntity();
            var e3 = state.CreateEntity();
            
            e1.Id.Should().NotBe(e2.Id);
            e2.Id.Should().NotBe(e3.Id);
            e1.Id.Should().NotBe(e3.Id);
        }

        [Fact]
        public void AddComponent_ShouldSetComponentMask()
        {
            var state = new GlobalState();
            var entity = state.CreateEntity();
            
            state.HasComponent<HealthComponent>(entity).Should().BeFalse();
            
            state.AddComponent(entity, new HealthComponent { CurrentHealth = 100 });
            
            state.HasComponent<HealthComponent>(entity).Should().BeTrue();
        }

        [Fact]
        public void RemoveComponent_ShouldUnsetMask()
        {
            var state = new GlobalState();
            var entity = state.CreateEntity();
            
            state.AddComponent(entity, new HealthComponent { CurrentHealth = 100 });
            state.HasComponent<HealthComponent>(entity).Should().BeTrue();
            
            state.RemoveComponent<HealthComponent>(entity);
            
            state.HasComponent<HealthComponent>(entity).Should().BeFalse();
        }

        [Fact]
        public void RemoveComponent_ShouldClearData()
        {
            var state = new GlobalState();
            var entity = state.CreateEntity();
            
            state.AddComponent(entity, new HealthComponent { CurrentHealth = 100 });
            state.RemoveComponent<HealthComponent>(entity);
            
            state.AddComponent(entity, new HealthComponent { CurrentHealth = 50 });
            
            state.GetComponent<HealthComponent>(entity).CurrentHealth.Value.Should().Be(50);
        }

        [Fact]
        public void Filter_ShouldReturnEntitiesWithAllComponents()
        {
            var state = new GlobalState();
            
            var e1 = state.CreateEntity();
            var e2 = state.CreateEntity();
            var e3 = state.CreateEntity();
            
            state.AddComponent(e1, new HealthComponent { CurrentHealth = 100 });
            state.AddComponent(e1, new RegionComponent { DamageCounter = 0 });
            
            state.AddComponent(e2, new HealthComponent { CurrentHealth = 50 });
            
            state.AddComponent(e3, new RegionComponent { DamageCounter = 10 });
            
            var filtered = state.Filter<HealthComponent, RegionComponent>().ToList();
            
            filtered.Should().HaveCount(1);
            filtered[0].Id.Should().Be(e1.Id);
        }

        [Fact]
        public void Filter_ShouldReturnEmptyWhenNoMatches()
        {
            var state = new GlobalState();
            
            var e1 = state.CreateEntity();
            state.AddComponent(e1, new HealthComponent { CurrentHealth = 100 });
            
            var filtered = state.Filter<HealthComponent, RegionComponent>().ToList();
            
            filtered.Should().BeEmpty();
        }

        [Fact]
        public void Filter_ShouldHandleLargeEntityCounts()
        {
            var state = new GlobalState();
            
            for (int i = 0; i < 1000; i++)
            {
                var entity = state.CreateEntity();
                state.AddComponent(entity, new HealthComponent { CurrentHealth = i });
                
                if (i % 2 == 0)
                {
                    state.AddComponent(entity, new RegionComponent { DamageCounter = i });
                }
            }
            
            var filtered = state.Filter<HealthComponent, RegionComponent>().ToList();
            
            filtered.Should().HaveCount(500);
        }

        [Fact]
        public void GetState_ShouldExpandCapacityAutomatically()
        {
            var state = new GlobalState();
            
            var entity = new Entity(500);
            
            ref var health = ref state.GetComponent<HealthComponent>(entity);
            health.CurrentHealth = 75;
            
            state.GetComponent<HealthComponent>(entity).CurrentHealth.Value.Should().Be(75);
        }

        [Fact]
        public void HasComponent_ShouldReturnFalseForNonExistentEntity()
        {
            var state = new GlobalState();
            
            var nonExistent = new Entity(999);
            
            state.HasComponent<HealthComponent>(nonExistent).Should().BeFalse();
        }

        [Fact]
        public void RegisterComponent_ShouldPreWarmTypeMetadata()
        {
            var state = new GlobalState();
            
            state.RegisterComponent<HealthComponent>();
            
            var array = state.GetRawArray<HealthComponent>();
            array.Should().NotBeNull();
            array.Length.Should().BeGreaterThan(0);
        }

        [Fact]
        public void MultipleComponents_ShouldCoexistOnSameEntity()
        {
            var state = new GlobalState();
            var entity = state.CreateEntity();
            
            state.AddComponent(entity, new HealthComponent { CurrentHealth = 100 });
            state.AddComponent(entity, new RegionComponent { DamageCounter = 5 });
            state.AddComponent(entity, new Party { PartyId = 1 });
            
            state.HasComponent<HealthComponent>(entity).Should().BeTrue();
            state.HasComponent<RegionComponent>(entity).Should().BeTrue();
            state.HasComponent<Party>(entity).Should().BeTrue();
            
            state.GetComponent<HealthComponent>(entity).CurrentHealth.Value.Should().Be(100);
            state.GetComponent<RegionComponent>(entity).DamageCounter.Should().Be(5);
            state.GetComponent<Party>(entity).PartyId.Should().Be(1);
        }
    }
}
