using System;
using Deterministic.GameFramework.CoreV2;
using Deterministic.GameFramework.CoreV2.Example.Components;
using FluentAssertions;
using Xunit;

namespace Deterministic.GameFramework.CoreV2.Tests
{
    public class ArchetypeObserverRollbackTests
    {
        [Fact]
        public void Observer_ShouldFireRemove_WhenStateIsResetToPriorTime()
        {
            // Setup
            var state = new GlobalState();
            state.RegisterComponent<HealthComponent>();
            var reactive = new ReactiveSystem();
            
            var addCount = 0;
            var removeCount = 0;

            var observer = reactive.ObserveArchetype<HealthComponent>(
                state,
                onAdd: (e) => addCount++,
                onRemove: (e) => removeCount++
            );

            // 1. Initial State (Clean)
            var entity = state.CreateEntity();
            // No component yet
            
            reactive.Tick(); // Flush
            addCount.Should().Be(0);

            // 2. Advance to "Future" (Dirty)
            state.AddComponent(entity, new HealthComponent { CurrentHealth = 100 });
            reactive.Tick();
            
            addCount.Should().Be(1);
            removeCount.Should().Be(0);

            // 3. Simulate Rollback (Restore State manually to Clean)
            // We manually remove the component to simulate "Restoring to Tick 0"
            state.RemoveComponent<HealthComponent>(entity);
            
            // CRITICAL: We must notify the observer that a "Reset" (Rollback) happened.
            // In the real system, GameLoop calls this. Here we call it manually to verify logic.
            // Accessing the observer directly via interface cast or we need to expose Reset on ReactiveSystem?
            // ReactiveSystem.Tick() calls Reset() if we toggle IsResimulating.
            
            // Simulate Resimulation Lifecycle
            // Start Resimulation
            // (Observer ignores updates)
            
            // End Resimulation -> Trigger Reset
            // We can't easily toggle private _wasResimulating in ReactiveSystem without mocking GameLoop.
            // But we can call observer.Reset() directly since we have the reference (casted).
            
            ((ObserverNode)observer).Reset();

            // Assert
            // The observer's internal bitset thinks it HAS the component (from Step 2).
            // The state (Step 3) DOES NOT have it.
            // Reset() -> FullScan() -> Check state -> Mismatch -> Fire OnRemove.
            
            removeCount.Should().Be(1);
        }

        [Fact]
        public void Observer_ShouldFireAdd_WhenStateIsResetToFutureTime()
        {
            // Reverse scenario: Rollback to a state where component existed, but currently doesn't?
            // (Less common in rollback, but valid for state jumps)
            
            var state = new GlobalState();
            state.RegisterComponent<HealthComponent>();
            var reactive = new ReactiveSystem();
            
            var entity = state.CreateEntity();
            var addCount = 0;
            
            var observer = reactive.ObserveArchetype<HealthComponent>(
                state,
                onAdd: (e) => addCount++,
                onRemove: (e) => { }
            );

            // 1. Initial State (No component)
            reactive.Tick();

            // 2. Simulate "Jump" to state with component (e.g. Loading a save)
            state.AddComponent(entity, new HealthComponent { CurrentHealth = 100 });
            
            // Manually Trigger Reset to force resync
            ((ObserverNode)observer).Reset();

            addCount.Should().Be(1);
        }
    }
}
