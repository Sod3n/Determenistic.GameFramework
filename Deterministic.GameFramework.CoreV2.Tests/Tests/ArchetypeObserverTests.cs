using System;
using System.Collections.Generic;
using Deterministic.GameFramework.CoreV2;
using Deterministic.GameFramework.CoreV2.Example.Components;
using Deterministic.GameFramework.Reactive;
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
                
            // var cards = reactive.ObservableCollection<CardComponent, OwnerComponent>();
            // var playerCards = cards.Where<OwnerComponent>(o => o.Owner == player);
            // playerCards.Subscribe(onAdd: (_) => addedCount++, onRemove: (_) => removedCount++).AddTo(Disposables);
            
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

        [Fact]
        public void ObserveArchetype_FluentApi_ShouldFilterCorrectly()
        {
            var state = new GlobalState();
            state.RegisterComponent<HealthComponent>();
            
            // Fix: Initialize GameLoop as ReactiveSystem.Bind requires it
            var scheduler = new ActionScheduler();
            var dispatcher = new Dispatcher();
            var gameLoop = new GameLoop(state, dispatcher, scheduler);
            
            var reactive = new ReactiveSystem();
            reactive.Bind(state); // Bind state for fluent API

            var addedCount = 0;
            var removedCount = 0;
            var lastAdded = -1;

            // Fluent API usage
            reactive.ObservableCollection<HealthComponent>()
                .Where<HealthComponent>(h => h.CurrentHealth > 50)
                .Subscribe(
                    onAdd: (e) => { addedCount++; lastAdded = e.Id; },
                    onRemove: (e) => { removedCount++; }
                );

            // 1. Add entity with Health 100 (Should Match)
            var e1 = state.CreateEntity();
            state.AddComponent(e1, new HealthComponent { CurrentHealth = 100 });
            reactive.Tick();

            addedCount.Should().Be(1);
            lastAdded.Should().Be(e1.Id);

            // 2. Add entity with Health 10 (Should NOT Match)
            var e2 = state.CreateEntity();
            state.AddComponent(e2, new HealthComponent { CurrentHealth = 10 });
            reactive.Tick();

            addedCount.Should().Be(1); // No change

            // 3. Update e2 to Health 60 (Should Match NOW)
            // Note: GlobalState requires MarkDirty for updates to be detected if we just modify ref
            ref var h2 = ref state.GetComponent<HealthComponent>(e2);
            h2.CurrentHealth = 60;
            // Manually mark dirty because we modified data in place and ReactiveSystem relies on dirty set
            // In a real scenario, systems should use state.MarkDirty(e2.Id) if they modify data that affects queries
            // Or use a wrapper that does it. 
            // However, ArchetypeObserver logic currently relies on GlobalState.GetDirtyEntities().
            // Let's ensure GlobalState marks it dirty or we do it manually.
            // GlobalState.GetComponent calls EnsureCapacity but doesn't necessarily mark dirty for *modification*
            // But AddComponent DOES mark dirty.
            // Let's force dirty for test purposes since we are modifying via ref.
            // Wait, GlobalState.GetComponent DOES mark dirty in the snippet I read earlier?
            // "MarkDirty(entity.Id); _entityMasks[entity.Id].Set(typeId);" inside GetComponent?
            // Let's check GlobalState again.
            // If it does, then modifying ref is enough IF we call GetComponent again.
            // If we held the ref from before, we need to mark dirty.
            // Here we call state.GetComponent again.
            
            // Re-read GlobalState.GetComponent... 
            // It calls EnsureTypedCapacity, EnsureEntityCapacity. 
            // It sets the mask bit.
            // It does NOT call MarkDirty explicitly in the snippet I saw (lines 88-100+). 
            // AddComponent calls MarkDirty.
            
            // So for this test to work with data modification, we might need to manually trigger dirty
            // or use AddComponent to overwrite.
            state.AddComponent(e2, new HealthComponent { CurrentHealth = 60 }); 
            reactive.Tick();

            addedCount.Should().Be(2);
            lastAdded.Should().Be(e2.Id);

            // 4. Update e1 to Health 0 (Should Unmatch)
            state.AddComponent(e1, new HealthComponent { CurrentHealth = 0 });
            reactive.Tick();

            removedCount.Should().Be(1);
        }
    }
}
