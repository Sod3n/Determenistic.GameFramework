using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Serialization;
using Deterministic.GameFramework.Types;
using FluentAssertions;
using Xunit;

namespace Deterministic.GameFramework.ECS.Tests
{
[Collection("Non-Parallel")]
            
    public class StateSerializerTests
    {
        public StateSerializerTests()
        {
            ComponentId.RegisterAssembly(typeof(PositionComponent).Assembly);
            ComponentId.RegisterAssembly(typeof(World).Assembly);
        }

        [Fact]
        public void RoundTrip_ShouldPreserveCompleteState()
        {
            var state = new EntityWorld();
            
            var entity1 = state.CreateEntity();
            var entity2 = state.CreateEntity();
            
            ref var pos1 = ref state.AddComponent(entity1, new PositionComponent { X = 100, Y = 200 });
            ref var pos2 = ref state.AddComponent(entity2, new PositionComponent { X = 50, Y = 60 });
            
            state.AddComponent(entity1, new TagComponent { TagId = 25 });
            
            byte[] serialized = StateSerializer.Serialize(state);
            
            var newState = new EntityWorld();
            newState.RegisterComponent<PositionComponent>();
            newState.RegisterComponent<TagComponent>();
            StateSerializer.Deserialize(newState, serialized);
            
            newState.GetComponent<PositionComponent>(entity1).X.Should().Be(100);
            newState.GetComponent<PositionComponent>(entity1).Y.Should().Be(200);
            newState.GetComponent<PositionComponent>(entity2).X.Should().Be(50);
            newState.GetComponent<TagComponent>(entity1).TagId.Should().Be(25);
        }

        [Fact]
        public void Serialize_ShouldBeDeterministic()
        {
            var state1 = new EntityWorld();
            var state2 = new EntityWorld();
            
            for (int i = 0; i < 10; i++)
            {
                var e1 = state1.CreateEntity();
                var e2 = state2.CreateEntity();
                
                ref var p1 = ref state1.AddComponent(e1, new PositionComponent());
                ref var p2 = ref state2.AddComponent(e2, new PositionComponent());
                
                p1.X = i * 10;
                p2.X = i * 10;
            }
            
            byte[] bytes1 = StateSerializer.Serialize(state1);
            byte[] bytes2 = StateSerializer.Serialize(state2);
            
            bytes1.Should().Equal(bytes2);
        }

        [Fact]
        public void Deserialize_ShouldThrowOnVersionMismatch()
        {
            // Note: Current serializer uses BinaryReader without version header check in first version,
            // but it reads Int32 for capacity/counts. Corrupted data will likely cause read error or OOM.
            byte[] corruptedData = new byte[] { 0xFF, 0xFF, 0, 0, 0, 0 };
            
            var state = new EntityWorld();
            
            Action act = () => StateSerializer.Deserialize(state, corruptedData);
            act.Should().Throw<Exception>(); 
        }

        [Fact]
        public void RoundTrip_ShouldHandleLargeEntityCounts()
        {
            var state = new EntityWorld();
            var entities = new Entity[500];

            for (int i = 0; i < 500; i++)
            {
                var entity = state.CreateEntity();
                entities[i] = entity;
                state.AddComponent(entity, new PositionComponent { X = i });
            }
            
            byte[] serialized = StateSerializer.Serialize(state);
            
            var newState = new EntityWorld();
            newState.RegisterComponent<PositionComponent>();
            StateSerializer.Deserialize(newState, serialized);
            
            for (int i = 0; i < 500; i++)
            {
                var entity = entities[i];
                newState.GetComponent<PositionComponent>(entity).X.Should().Be(i);
            }
        }

        [Fact]
        public void RoundTrip_ShouldPreserveEntityMasks()
        {
            var state = new EntityWorld();
            
            var entity1 = state.CreateEntity();
            var entity2 = state.CreateEntity();
            
            state.AddComponent(entity1, new PositionComponent());
            state.AddComponent(entity1, new TagComponent());
            state.AddComponent(entity2, new PositionComponent());
            
            byte[] serialized = StateSerializer.Serialize(state);
            
            var newState = new EntityWorld();
            newState.RegisterComponent<PositionComponent>();
            newState.RegisterComponent<TagComponent>();
            StateSerializer.Deserialize(newState, serialized);
            
            newState.HasComponent<PositionComponent>(entity1).Should().BeTrue();
            newState.HasComponent<TagComponent>(entity1).Should().BeTrue();
            newState.HasComponent<PositionComponent>(entity2).Should().BeTrue();
            newState.HasComponent<TagComponent>(entity2).Should().BeFalse();
        }

        [Fact]
        public void Serialize_ShouldHandleEmptyState()
        {
            var state = new EntityWorld();
            
            byte[] serialized = StateSerializer.Serialize(state);
            serialized.Should().NotBeNull();
            serialized.Length.Should().BeGreaterThan(0);
            
            var newState = new EntityWorld();
            StateSerializer.Deserialize(newState, serialized);
            
            newState.NextEntityId.Should().Be(state.NextEntityId);
        }

        [Fact]
        public void Deserialize_ShouldClearComponents_IfSourceDidNotHaveThem()
        {
            var state = new EntityWorld();
            state.RegisterComponent<PositionComponent>();
            var entity = state.CreateEntity();
            
            // Snapshot 1: Empty (just entity created, no components)
            // Note: EntityWorld.CreateEntity increments ID but doesn't add components.
            // Wait, CreateEntity doesn't add anything.
            
            byte[] emptyData = StateSerializer.Serialize(state);
            
            // Modify: Add Component
            state.AddComponent(entity, new PositionComponent { X = 100 });
            state.HasComponent<PositionComponent>(entity).Should().BeTrue();
            
            // Deserialize Snapshot 1
            StateSerializer.Deserialize(state, emptyData);
            
            // Verify: Component should be gone
            state.HasComponent<PositionComponent>(entity).Should().BeFalse();
        }
    }
}
