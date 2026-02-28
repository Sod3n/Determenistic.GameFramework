using System;
using System.Runtime.InteropServices;
using Deterministic.GameFramework.CoreV2;
using Deterministic.GameFramework.CoreV2.Example.Actions;
using Deterministic.GameFramework.CoreV2.Example.Components;
using Deterministic.GameFramework.CoreV2.Example.Services;
using Deterministic.GameFramework.CoreV2.Example.Reactions;
using FluentAssertions;
using Xunit;

namespace Deterministic.GameFramework.CoreV2.Tests
{
    public class ActionSchedulerTests
    {
        [Fact]
        public void EarliestDirtyTick_ShouldTrackMinimumScheduledTick()
        {
            var scheduler = new ActionScheduler();
            
            scheduler.EarliestDirtyTick.Should().Be(long.MaxValue);
            
            var action = new DamageAction(10);
            scheduler.Schedule(action, 1, new Entity(1), 5);
            scheduler.EarliestDirtyTick.Should().Be(5);
            
            scheduler.Schedule(action, 1, new Entity(2), 3);
            scheduler.EarliestDirtyTick.Should().Be(3);
            
            scheduler.Schedule(action, 1, new Entity(3), 10);
            scheduler.EarliestDirtyTick.Should().Be(3);
        }

        [Fact]
        public void ScheduleFromBytes_ShouldUpdateDirtyTick()
        {
            var scheduler = new ActionScheduler();
            
            var action = new DamageAction(15);
            int size = Marshal.SizeOf<DamageAction>();
            byte[] bytes = new byte[size];
            MemoryMarshal.Write(bytes, in action);
            
            scheduler.ScheduleFromBytes(1, bytes, 1, 7);
            scheduler.EarliestDirtyTick.Should().Be(7);
        }

        [Fact]
        public void ExecuteActions_ShouldResetDirtyTickAfterExecution()
        {
            var state = new GlobalState();
            var dispatcher = new Dispatcher();
            dispatcher.RegisterAction<DamageAction, HealthComponent>(new DamageActionHandler(), Array.Empty<DecreaseDamageReaction>());
            
            var scheduler = new ActionScheduler();
            var entity = state.CreateEntity();
            state.GetComponent<HealthComponent>(entity).CurrentHealth = 100;
            
            var action = new DamageAction(10);
            scheduler.Schedule(action, 1, entity, 5);
            
            scheduler.EarliestDirtyTick.Should().Be(5);
            
            scheduler.ExecuteActions(5, state, dispatcher);
            
            scheduler.EarliestDirtyTick.Should().Be(long.MaxValue);
        }

        [Fact]
        public void ExecuteActions_ShouldExecuteInDeterministicOrder()
        {
            var state = new GlobalState();
            var dispatcher = new Dispatcher();
            dispatcher.RegisterAction<DamageAction, HealthComponent>(new DamageActionHandler(), Array.Empty<DecreaseDamageReaction>());
            
            var scheduler = new ActionScheduler();
            
            var e1 = state.CreateEntity();
            var e2 = state.CreateEntity();
            var e3 = state.CreateEntity();
            
            state.GetComponent<HealthComponent>(e1).CurrentHealth = 100;
            state.GetComponent<HealthComponent>(e2).CurrentHealth = 100;
            state.GetComponent<HealthComponent>(e3).CurrentHealth = 100;
            
            scheduler.Schedule(new DamageAction(30), 1, e3, 10);
            scheduler.Schedule(new DamageAction(10), 1, e1, 10);
            scheduler.Schedule(new DamageAction(20), 1, e2, 10);
            
            scheduler.ExecuteActions(10, state, dispatcher);
            
            state.GetComponent<HealthComponent>(e1).CurrentHealth.Value.Should().Be(90);
            state.GetComponent<HealthComponent>(e2).CurrentHealth.Value.Should().Be(80);
            state.GetComponent<HealthComponent>(e3).CurrentHealth.Value.Should().Be(70);
        }

        [Fact]
        public void PruneHistory_ShouldRemoveOldActions()
        {
            var scheduler = new ActionScheduler();
            
            var action = new DamageAction(10);
            scheduler.Schedule(action, 1, new Entity(1), 5);
            scheduler.Schedule(action, 1, new Entity(2), 10);
            scheduler.Schedule(action, 1, new Entity(3), 15);
            
            scheduler.PruneHistory(12);
            
            scheduler.EarliestDirtyTick.Should().Be(15);
        }

        [Fact]
        public void PruneHistory_ShouldResetDirtyTickWhenAllPruned()
        {
            var scheduler = new ActionScheduler();
            
            var action = new DamageAction(10);
            scheduler.Schedule(action, 1, new Entity(1), 5);
            scheduler.Schedule(action, 1, new Entity(2), 10);
            
            scheduler.PruneHistory(20);
            
            scheduler.EarliestDirtyTick.Should().Be(long.MaxValue);
        }

        [Fact]
        public void OnActionScheduled_ShouldFireEvent()
        {
            var scheduler = new ActionScheduler();
            
            bool eventFired = false;
            int capturedNetworkId = 0;
            long capturedTick = 0;
            
            scheduler.OnActionScheduled += (networkId, data, targetId, tick) =>
            {
                eventFired = true;
                capturedNetworkId = networkId;
                capturedTick = tick;
            };
            
            var action = new DamageAction(10);
            scheduler.Schedule(action, 1, new Entity(5), 7);
            
            eventFired.Should().BeTrue();
            capturedNetworkId.Should().Be(1);
            capturedTick.Should().Be(7);
        }

        [Fact]
        public void ExecuteActions_ShouldOnlyExecuteActionsForSpecificTick()
        {
            var state = new GlobalState();
            var dispatcher = new Dispatcher();
            dispatcher.RegisterAction<DamageAction, HealthComponent>(new DamageActionHandler(), Array.Empty<DecreaseDamageReaction>());
            
            var scheduler = new ActionScheduler();
            var entity = state.CreateEntity();
            state.GetComponent<HealthComponent>(entity).CurrentHealth = 100;
            
            scheduler.Schedule(new DamageAction(10), 1, entity, 5);
            scheduler.Schedule(new DamageAction(20), 1, entity, 10);
            
            scheduler.ExecuteActions(5, state, dispatcher);
            
            state.GetComponent<HealthComponent>(entity).CurrentHealth.Value.Should().Be(90);
            
            scheduler.ExecuteActions(10, state, dispatcher);
            
            state.GetComponent<HealthComponent>(entity).CurrentHealth.Value.Should().Be(70);
        }
    }
}
