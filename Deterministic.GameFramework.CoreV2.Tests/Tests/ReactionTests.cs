using Deterministic.GameFramework.CoreV2;
using Deterministic.GameFramework.CoreV2.Example.Actions;
using Deterministic.GameFramework.CoreV2.Example.Components;
using Deterministic.GameFramework.CoreV2.Example.Reactions;
using Deterministic.GameFramework.CoreV2.Example.Services;
using FluentAssertions;
using Xunit;

namespace Deterministic.GameFramework.CoreV2.Tests
{
    public class ReactionTests
    {
        private GlobalState _state;
        private ActionScheduler _scheduler;
        private Dispatcher _dispatcher;
        private GameLoop _gameLoop;

        public ReactionTests()
        {
            _state = new GlobalState();
            _scheduler = new ActionScheduler();
            _dispatcher = new Dispatcher();
            _gameLoop = new GameLoop(_state, _dispatcher, _scheduler);
        }

        [Fact]
        public void LocalReaction_ShouldExecute_WhenTagIsPresent()
        {
            // Arrange
            var damageHandler = new DamageActionHandler();
            var reactions = new[] { new DecreaseDamageReaction() };
            _dispatcher.RegisterAction<DamageAction, HealthComponent>(damageHandler, reactions);
            _dispatcher.RegisterReaction(new RegionDamageReaction());

            var player = new Entity(2);
            ref var health = ref _state.GetState<HealthComponent>(player);
            health.CurrentHealth = 100;
            
            // Add Party component (required by RegionDamageReaction logic)
            _state.AddComponent(player, new Party { PartyId = 1 });
            
            // Add RegionComponent to Player
            _state.AddComponent(player, new RegionComponent { DamageCounter = 0 });
            
            // Add Reaction Tag to Player
            player.AddReaction(_state, new RegionDamageReactionTag { TargetParty = new Party { PartyId = 1 } });

            // Act
            _gameLoop.Schedule(new DamageAction(50), player);
            _gameLoop.RunSingleTick();

            // Assert
            var playerRegion = _state.GetState<RegionComponent>(player);
            playerRegion.DamageCounter.Should().Be(50, "Local reaction should update the component on the entity itself");
        }

        [Fact]
        public void HierarchyBubbling_ShouldNotOccur_ByDefault()
        {
            // Arrange
            var damageHandler = new DamageActionHandler();
            var reactions = new[] { new DecreaseDamageReaction() };
            _dispatcher.RegisterAction<DamageAction, HealthComponent>(damageHandler, reactions);
            _dispatcher.RegisterReaction(new RegionDamageReaction());

            var rootNode = new Entity(1);
            var player = new Entity(2);
            
            // Setup Hierarchy
            _state.GetState<HierarchyComponent>(rootNode);
            _state.GetState<HierarchyComponent>(player);
            rootNode.AddChild(player, _state);

            // Add RegionComponent to Root
            _state.AddComponent(rootNode, new RegionComponent { DamageCounter = 0 });
            
            // Add Reaction Tag to Root (targetting party 1)
            rootNode.AddReaction(_state, new RegionDamageReactionTag { TargetParty = new Party { PartyId = 1 } });

            // Setup Player
            ref var health = ref _state.GetState<HealthComponent>(player);
            health.CurrentHealth = 100;
            _state.AddComponent(player, new Party { PartyId = 1 });

            // Act
            // Damage the PLAYER
            _gameLoop.Schedule(new DamageAction(50), player);
            _gameLoop.RunSingleTick();

            // Assert
            var rootRegion = _state.GetState<RegionComponent>(rootNode);
            // In the PoC, it specifically says "With hierarchy bubbling removed, this tag on rootNode will NOT trigger for actions on player."
            rootRegion.DamageCounter.Should().Be(0, "Events should not bubble up the hierarchy automatically");
        }
    }
}
