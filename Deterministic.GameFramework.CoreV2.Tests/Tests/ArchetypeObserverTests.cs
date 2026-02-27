using System;
using System.Collections.Generic;
using Deterministic.GameFramework.CoreV2;
using Deterministic.GameFramework.CoreV2.Example.Components;
using FluentAssertions;
using Xunit;

namespace Deterministic.GameFramework.CoreV2.Tests
{
    public class ArchetypeObserverTests
    {
        [Fact]
        public void ObserveArchetype_ShouldDetectExistingEntities()
        {
            var state = new GlobalState();
            state.RegisterComponent<HealthComponent>();

            var e1 = state.CreateEntity();
            state.AddComponent(e1, new HealthComponent { CurrentHealth = 100 });

            var reactive = new ReactiveSystem();
            var addedEntities = new List<Entity>();
            
            reactive.ObserveArchetype<HealthComponent>(
                state, 
                onAdd: (e) => addedEntities.Add(e), 
                onRemove: (_) => { });

            // Should detect immediately upon registration/first tick if the implementation does check on init
            // Looking at ArchetypeObserver.Initialize, it calls CheckAndNotify() immediately.
            
            addedEntities.Should().ContainSingle();
            addedEntities[0].Id.Should().Be(e1.Id);
        }

        [Fact]
        public void ObserveArchetype_ShouldDetectNewEntities_OnNextTick()
        {
            var state = new GlobalState();
            state.RegisterComponent<HealthComponent>();
            var reactive = new ReactiveSystem();
            
            var addedEntities = new List<Entity>();
            reactive.ObserveArchetype<HealthComponent>(
                state, 
                onAdd: (e) => addedEntities.Add(e), 
                onRemove: (_) => { });

            var e1 = state.CreateEntity();
            state.AddComponent(e1, new HealthComponent { CurrentHealth = 100 });

            addedEntities.Should().BeEmpty(); // Not detected yet

            reactive.Tick();

            addedEntities.Should().ContainSingle();
            addedEntities[0].Id.Should().Be(e1.Id);
        }

        [Fact]
        public void ObserveArchetype_ShouldDetectComponentRemoval()
        {
            var state = new GlobalState();
            state.RegisterComponent<HealthComponent>();
            var reactive = new ReactiveSystem();
            
            var e1 = state.CreateEntity();
            state.AddComponent(e1, new HealthComponent { CurrentHealth = 100 });

            var removedEntities = new List<Entity>();
            reactive.ObserveArchetype<HealthComponent>(
                state, 
                onAdd: (_) => { }, 
                onRemove: (e) => removedEntities.Add(e));

            // Initial tick to register existence
            reactive.Tick(); 
            
            state.RemoveComponent<HealthComponent>(e1);
            
            removedEntities.Should().BeEmpty(); // Not detected yet

            reactive.Tick();

            removedEntities.Should().ContainSingle();
            removedEntities[0].Id.Should().Be(e1.Id);
        }

        [Fact]
        public void ObserveArchetype_MultiComponent_ShouldMatchOnlyExactSet()
        {
            var state = new GlobalState();
            state.RegisterComponent<HealthComponent>();
            state.RegisterComponent<RegionComponent>();
            
            var reactive = new ReactiveSystem();
            var addedEntities = new List<Entity>();

            reactive.ObserveArchetype<HealthComponent, RegionComponent>(
                state, 
                onAdd: (e) => addedEntities.Add(e), 
                onRemove: (_) => { });

            var e1 = state.CreateEntity();
            state.AddComponent(e1, new HealthComponent());
            
            var e2 = state.CreateEntity();
            state.AddComponent(e2, new RegionComponent());

            var e3 = state.CreateEntity();
            state.AddComponent(e3, new HealthComponent());
            state.AddComponent(e3, new RegionComponent());

            reactive.Tick();

            addedEntities.Should().ContainSingle();
            addedEntities[0].Id.Should().Be(e3.Id);
        }

        [Fact]
        public void ObserveArchetype_ShouldHandleEntityReuse()
        {
            // Note: This test assumes GlobalState reuses IDs or we just manually simulate reuse logic if possible.
            // Since we don't control ID reuse explicitly easily without exhausting IDs, we'll just check if
            // adding/removing components on same ID works repeatedly.

            var state = new GlobalState();
            state.RegisterComponent<HealthComponent>();
            var reactive = new ReactiveSystem();
            
            var e1 = state.CreateEntity();
            var addedCount = 0;
            var removedCount = 0;

            reactive.ObserveArchetype<HealthComponent>(
                state,
                onAdd: (_) => addedCount++,
                onRemove: (_) => removedCount++);

            // Add
            state.AddComponent(e1, new HealthComponent());
            reactive.Tick();
            addedCount.Should().Be(1);

            // Remove
            state.RemoveComponent<HealthComponent>(e1);
            reactive.Tick();
            removedCount.Should().Be(1);

            // Add again (Reuse)
            state.AddComponent(e1, new HealthComponent());
            reactive.Tick();
            addedCount.Should().Be(2);
        }

        [Fact]
        public void Dispose_ShouldStopNotifications()
        {
            var state = new GlobalState();
            state.RegisterComponent<HealthComponent>();
            var reactive = new ReactiveSystem();
            
            var addedCount = 0;
            var sub = reactive.ObserveArchetype<HealthComponent>(
                state,
                onAdd: (_) => addedCount++,
                onRemove: (_) => { });

            var e1 = state.CreateEntity();
            state.AddComponent(e1, new HealthComponent());
            reactive.Tick();
            addedCount.Should().Be(1);

            sub.Dispose();

            var e2 = state.CreateEntity();
            state.AddComponent(e2, new HealthComponent());
            reactive.Tick();
            
            addedCount.Should().Be(1); // Should not increase
        }
    }
}
