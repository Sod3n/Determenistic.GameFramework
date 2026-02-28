using System;
using System.Runtime.InteropServices;
using Deterministic.GameFramework.CoreV2;
using Deterministic.GameFramework.CoreV2.Example.Actions;
using Deterministic.GameFramework.CoreV2.Example.Components;
using Deterministic.GameFramework.CoreV2.Example.Reactions;
using Deterministic.GameFramework.CoreV2.Example.Services;
using FluentAssertions;
using Xunit;

namespace Deterministic.GameFramework.CoreV2.Tests
{
    public class GameLoopTests
    {
        private GlobalState _state;
        private ActionScheduler _scheduler;
        private Dispatcher _dispatcher;
        private GameLoop _gameLoop;

        public GameLoopTests()
        {
            _state = new GlobalState();
            _scheduler = new ActionScheduler();
            _dispatcher = new Dispatcher();

            _gameLoop = new GameLoop(_state, _dispatcher, _scheduler);
            _gameLoop.SetTickRate(60);
        }

        [Fact]
        public void ScheduledActions_ShouldExecute_OnCorrectTick()
        {
            // Arrange
            var damageHandler = new DamageActionHandler();
            var reactions = new[] { new DecreaseDamageReaction() };
            _dispatcher.RegisterAction<DamageAction, HealthComponent>(damageHandler, reactions);

            var player = new Entity(2);
            ref var health = ref _state.GetComponent<HealthComponent>(player);
            health.CurrentHealth = 100;

            // Act
            // Schedule immediate damage (next tick)
            _gameLoop.Schedule(new DamageAction(15), player);
            
            // Run 1 tick
            _gameLoop.RunSingleTick();

            // Assert
            var healthAfter = _state.GetComponent<HealthComponent>(player).CurrentHealth;
            healthAfter.Value.Should().Be(85); // 100 - 15
        }

        [Fact]
        public void FutureScheduledActions_ShouldExecute_OnFutureTick()
        {
            // Arrange
            var damageHandler = new DamageActionHandler();
            var reactions = new[] { new DecreaseDamageReaction() };
            _dispatcher.RegisterAction<DamageAction, HealthComponent>(damageHandler, reactions);

            var player = new Entity(2);
            ref var health = ref _state.GetComponent<HealthComponent>(player);
            health.CurrentHealth = 100;

            // Act
            // Schedule future damage on tick 3
            _gameLoop.ScheduleOnTick(3, new DamageAction(25), player);

            // Run ticks 0, 1, 2 (current tick starts at 0, incremented at start of RunSingleTick? or end?)
            // Usually GameLoop starts at 0. RunSingleTick processes tick 1, etc.
            // Let's assume standard behavior:
            // Tick 0: Processing...
            // Tick 1: Processing...
            // Tick 2: Processing...
            // Tick 3: Processing (Action executes here)
            
            // We need to advance THROUGH tick 3.
            // CurrentTick starts at 0.
            // RunSingleTick() processes the CurrentTick and increments it.
            // We want to process Tick 3.
            while (_gameLoop.CurrentTick <= 3)
            {
                _gameLoop.RunSingleTick();
            }

            // Assert
            // At end of loop, CurrentTick is 4. Tick 3 actions have executed.
            var healthAfter = _state.GetComponent<HealthComponent>(player).CurrentHealth;
            healthAfter.Value.Should().Be(75); // 100 - 25
        }

        [Fact]
        public void LatePacket_ShouldTrigger_RollbackAndResimulation()
        {
            // Arrange
            var damageHandler = new DamageActionHandler();
            var reactions = new[] { new DecreaseDamageReaction() };
            _dispatcher.RegisterAction<DamageAction, HealthComponent>(damageHandler, reactions);

            var player = new Entity(2);
            ref var health = ref _state.GetComponent<HealthComponent>(player);
            health.CurrentHealth = 100;

            // Schedule initial actions to build state history
            _gameLoop.Schedule(new DamageAction(15), player); // Tick 1 (Next)
            _gameLoop.ScheduleOnTick(3, new DamageAction(25), player); // Tick 3

            // Act: Run until Tick 10
            while (_gameLoop.CurrentTick < 10)
            {
                _gameLoop.RunSingleTick();
            }

            // Verify state at Tick 10 before rollback
            // 100 - 15 (Tick 1) - 25 (Tick 3) = 60
            _state.GetComponent<HealthComponent>(player).CurrentHealth.Value.Should().Be(60);

            // Act: Inject LATE packet for Tick 5
            var lateDamage = new DamageAction(10);
            int dSize = Marshal.SizeOf<DamageAction>();
            byte[] dBytes = new byte[dSize];
            MemoryMarshal.Write(dBytes, in lateDamage);

            // Schedule for Tick 5 (past)
            _scheduler.ScheduleFromBytes(1, dBytes, player.Id, 5);

            // Run Tick 11 - This should trigger Rollback!
            _gameLoop.RunSingleTick();

            // Assert
            // Calculation:
            // Start 100
            // Tick 1: -15 -> 85
            // Tick 3: -25 -> 60
            // Tick 5: -10 (Late) -> 50
            // Result should be 50
            
            var finalHealth = _state.GetComponent<HealthComponent>(player).CurrentHealth;
            finalHealth.Value.Should().Be(50);
        }
    }
}
