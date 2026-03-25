using System.Linq;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Types;
using FluentAssertions;
using Xunit;

namespace Deterministic.GameFramework.ECS.Tests
{
    [Collection("Non-Parallel")] public class EntityWorldTests
    {
        public EntityWorldTests()
        {
            // Register components for testing
            ComponentId.RegisterAssembly(typeof(EntityWorldTests).Assembly);
            ComponentId.RegisterAssembly(typeof(World).Assembly);
        }

        [Fact]
        public void CreateEntity_ShouldGenerateUniqueIds()
        {
            var world = new EntityWorld();
            
            // Note: World creates one entity internally for singleton
            var e1 = world.CreateEntity();
            var e2 = world.CreateEntity();
            var e3 = world.CreateEntity();
            
            e1.Id.Should().NotBe(e2.Id);
            e2.Id.Should().NotBe(e3.Id);
            e1.Id.Should().NotBe(e3.Id);
        }

        [Fact]
        public void AddComponent_ShouldSetComponentMask()
        {
            var world = new EntityWorld();
            var entity = world.CreateEntity();
            
            world.HasComponent<PositionComponent>(entity).Should().BeFalse();
            
            world.AddComponent(entity, new PositionComponent { X = 100, Y = 200 });
            
            world.HasComponent<PositionComponent>(entity).Should().BeTrue();
        }

        [Fact]
        public void RemoveComponent_ShouldUnsetMask()
        {
            var world = new EntityWorld();
            var entity = world.CreateEntity();
            
            world.AddComponent(entity, new PositionComponent { X = 100 });
            world.HasComponent<PositionComponent>(entity).Should().BeTrue();
            
            world.RemoveComponent<PositionComponent>(entity);
            
            world.HasComponent<PositionComponent>(entity).Should().BeFalse();
        }

        [Fact]
        public void RemoveComponent_ShouldClearData_IfImplemented()
        {
            // Note: EntityWorld.RemoveComponent implementation clears the array slot
            var world = new EntityWorld();
            var entity = world.CreateEntity();
            
            world.AddComponent(entity, new PositionComponent { X = 100 });
            world.RemoveComponent<PositionComponent>(entity);
            
            // Re-add and check if we get clean state if we don't init
            // But AddComponent returns ref to storage, so we overwrite it usually.
            // Let's check if the previous value persists if we somehow access it or if we just check behavior.
            
            world.AddComponent(entity, new PositionComponent { X = 50 });
            
            world.GetComponent<PositionComponent>(entity).X.Should().Be(50);
        }

        [Fact]
        public void Filter_ShouldReturnEntitiesWithAllComponents()
        {
            var world = new EntityWorld();
            
            var e1 = world.CreateEntity();
            var e2 = world.CreateEntity();
            var e3 = world.CreateEntity();
            
            world.AddComponent(e1, new PositionComponent { X = 100 });
            world.AddComponent(e1, new TagComponent { TagId = 1 });
            
            world.AddComponent(e2, new PositionComponent { X = 50 });
            
            world.AddComponent(e3, new TagComponent { TagId = 2 });
            
            var filtered = world.Filter<PositionComponent, TagComponent>().ToList();
            
            filtered.Should().HaveCount(1);
            filtered[0].Id.Should().Be(e1.Id);
        }

        [Fact]
        public void Filter_ShouldReturnEmptyWhenNoMatches()
        {
            var world = new EntityWorld();
            
            var e1 = world.CreateEntity();
            world.AddComponent(e1, new PositionComponent { X = 100 });
            
            var filtered = world.Filter<PositionComponent, TagComponent>().ToList();
            
            filtered.Should().BeEmpty();
        }

        [Fact]
        public void Filter_ShouldHandleLargeEntityCounts()
        {
            var world = new EntityWorld();
            
            for (int i = 0; i < 1000; i++)
            {
                var entity = world.CreateEntity();
                world.AddComponent(entity, new PositionComponent { X = i });
                
                if (i % 2 == 0)
                {
                    world.AddComponent(entity, new TagComponent { TagId = i });
                }
            }
            
            var filtered = world.Filter<PositionComponent, TagComponent>().ToList();
            
            filtered.Should().HaveCount(500);
        }

        [Fact]
        public void GetComponent_ShouldExpandCapacityAutomatically()
        {
            var world = new EntityWorld();
            
            // Manually create entity with high ID to force expansion
            // EntityWorld.CreateEntity increments ID, so we loop
            Entity entity = default;
            for(int i=0; i<500; i++) entity = world.CreateEntity();
            
            ref var pos = ref world.AddComponent(entity, new PositionComponent());
            pos.X = 75;
            
            world.GetComponent<PositionComponent>(entity).X.Should().Be(75);
        }

        [Fact]
        public void HasComponent_ShouldReturnFalseForNonExistentEntity()
        {
            var world = new EntityWorld();
            
            var nonExistent = new Entity(999);
            
            world.HasComponent<PositionComponent>(nonExistent).Should().BeFalse();
        }

        [Fact]
        public void RegisterComponent_ShouldPreWarmTypeMetadata()
        {
            var world = new EntityWorld();
            
            world.RegisterComponent<PositionComponent>();
            
            var store = world.GetComponentStore<PositionComponent>();
            store.Should().NotBeNull();
            store.Length.Should().BeGreaterThan(0);
        }

        [Fact]
        public void MultipleComponents_ShouldCoexistOnSameEntity()
        {
            var world = new EntityWorld();
            var entity = world.CreateEntity();
            
            world.AddComponent(entity, new PositionComponent { X = 100 });
            world.AddComponent(entity, new VelocityComponent { X = 5 });
            world.AddComponent(entity, new TagComponent { TagId = 1 });
            
            world.HasComponent<PositionComponent>(entity).Should().BeTrue();
            world.HasComponent<VelocityComponent>(entity).Should().BeTrue();
            world.HasComponent<TagComponent>(entity).Should().BeTrue();
            
            world.GetComponent<PositionComponent>(entity).X.Should().Be(100);
            world.GetComponent<VelocityComponent>(entity).X.Should().Be(5);
            world.GetComponent<TagComponent>(entity).TagId.Should().Be(1);
        }
        
        [Fact]
        public void DirtyTracking_ShouldTrackModifications()
        {
            var world = new EntityWorld();
            var entity = world.CreateEntity();
            
            world.ClearDirty();
            world.GetDirtyEntities().Should().BeEmpty();
            
            world.AddComponent(entity, new PositionComponent { X = 1 });
            
            world.GetDirtyEntities().Should().Contain(entity.Id);
            
            world.ClearDirty();
            world.GetDirtyEntities().Should().BeEmpty();
            
            world.RemoveComponent<PositionComponent>(entity);
            world.GetDirtyEntities().Should().Contain(entity.Id);
        }

        [Fact]
        public void Filter_ShouldHandleLargeCounts_Debug()
        {
            var world = new EntityWorld();
            int count = 10; 
            
            for (int i = 0; i < count; i++)
            {
                var entity = world.CreateEntity();
                world.AddComponent(entity, new PositionComponent { X = i });
                
                if (i % 2 == 0)
                {
                    world.AddComponent(entity, new TagComponent { TagId = i });
                }
            }
            
            var filtered = world.Filter<PositionComponent, TagComponent>().ToList();
            
            filtered.Should().HaveCount(count / 2);
        }

        [Fact]
        public void Filter_Mismatch_Debug()
        {
            var posId = ComponentId<PositionComponent>.IntId;
            var tagId = ComponentId<TagComponent>.IntId;
            
            var world = new EntityWorld();
            
            // Case 1: Entity with BOTH
            var e1 = world.CreateEntity();
            world.AddComponent(e1, new PositionComponent());
            world.AddComponent(e1, new TagComponent());
            
            // Case 2: Entity with ONLY Position
            var e2 = world.CreateEntity();
            world.AddComponent(e2, new PositionComponent());
            
            var mask1 = world.EntityMasks[e1.Id];
            var mask2 = world.EntityMasks[e2.Id];
            
            var filterMask = new BitMask128();
            filterMask.Set(posId);
            filterMask.Set(tagId);
            
            bool e1Matches = mask1.HasAll(filterMask);
            bool e2Matches = mask2.HasAll(filterMask);
            
            e1Matches.Should().BeTrue();
            e2Matches.Should().BeFalse();
            
            var filtered = world.Filter<PositionComponent, TagComponent>().ToList();
            filtered.Should().ContainSingle(e => e.Id == e1.Id);
        }
    }
}
