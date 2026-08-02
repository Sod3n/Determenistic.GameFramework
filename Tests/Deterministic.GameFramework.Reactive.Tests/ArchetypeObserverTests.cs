using System;
using System.Collections.Generic;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Reactive;
using Deterministic.GameFramework.Common;
using Deterministic.GameFramework.DAR;
using Deterministic.GameFramework.Serialization;
using Deterministic.GameFramework.Types;
using FluentAssertions;
using Xunit;

namespace Deterministic.GameFramework.ECS.Tests
{
    [Collection("Non-Parallel")] public class ArchetypeObserverTests
    {
        public ArchetypeObserverTests()
        {
            ComponentId.RegisterAssembly(typeof(PositionComponent).Assembly);
            ComponentId.RegisterAssembly(typeof(World).Assembly);
        }

        [Fact]
        public void ObserveArchetype_ShouldDetectExistingEntities()
        {
            var state = new EntityWorld();
            state.RegisterComponent<PositionComponent>();

            var e1 = state.CreateEntity();
            state.AddComponent(e1, new PositionComponent { X = 100 });

            var reactive = new ReactiveSystem();
            var addedEntities = new List<Entity>();
            
            reactive.ObserveArchetype<PositionComponent>(
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
            var state = new EntityWorld();
            state.RegisterComponent<PositionComponent>();
            var reactive = new ReactiveSystem();
            
            var addedEntities = new List<Entity>();
            reactive.ObserveArchetype<PositionComponent>(
                state, 
                onAdd: (e) => addedEntities.Add(e), 
                onRemove: (_) => { });

            var e1 = state.CreateEntity();
            state.AddComponent(e1, new PositionComponent { X = 100 });

            addedEntities.Should().BeEmpty(); // Not detected yet

            reactive.Tick();

            addedEntities.Should().ContainSingle();
            addedEntities[0].Id.Should().Be(e1.Id);
        }

        [Fact]
        public void ObserveArchetype_ShouldDetectComponentRemoval()
        {
            var state = new EntityWorld();
            state.RegisterComponent<PositionComponent>();
            var reactive = new ReactiveSystem();
            
            var e1 = state.CreateEntity();
            state.AddComponent(e1, new PositionComponent { X = 100 });

            var removedEntities = new List<Entity>();
            reactive.ObserveArchetype<PositionComponent>(
                state, 
                onAdd: (_) => { }, 
                onRemove: (e) => removedEntities.Add(e));

            // Initial tick to register existence
            reactive.Tick(); 
            
            state.RemoveComponent<PositionComponent>(e1);
            
            removedEntities.Should().BeEmpty(); // Not detected yet

            reactive.Tick();

            removedEntities.Should().ContainSingle();
            removedEntities[0].Id.Should().Be(e1.Id);
        }

        [Fact]
        public void ObserveArchetype_MultiComponent_ShouldMatchOnlyExactSet()
        {
            var state = new EntityWorld();
            state.RegisterComponent<PositionComponent>();
            state.RegisterComponent<TagComponent>();
            
            var reactive = new ReactiveSystem();
            var addedEntities = new List<Entity>();

            reactive.ObserveArchetype<PositionComponent, TagComponent>(
                state, 
                onAdd: (e) => addedEntities.Add(e), 
                onRemove: (_) => { });

            var e1 = state.CreateEntity();
            state.AddComponent(e1, new PositionComponent());
            
            var e2 = state.CreateEntity();
            state.AddComponent(e2, new TagComponent());

            var e3 = state.CreateEntity();
            state.AddComponent(e3, new PositionComponent());
            state.AddComponent(e3, new TagComponent());

            reactive.Tick();

            addedEntities.Should().ContainSingle();
            addedEntities[0].Id.Should().Be(e3.Id);
        }

        [Fact]
        public void ObserveArchetype_ShouldHandleEntityReuse()
        {
            // Note: This test assumes EntityWorld reuses IDs or we just manually simulate reuse logic if possible.
            // Since we don't control ID reuse explicitly easily without exhausting IDs, we'll just check if
            // adding/removing components on same ID works repeatedly.

            var state = new EntityWorld();
            state.RegisterComponent<PositionComponent>();
            var reactive = new ReactiveSystem();
            
            var e1 = state.CreateEntity();
            var addedCount = 0;
            var removedCount = 0;

            reactive.ObserveArchetype<PositionComponent>(
                state,
                onAdd: (_) => addedCount++,
                onRemove: (_) => removedCount++);
                
            // Add
            state.AddComponent(e1, new PositionComponent());
            reactive.Tick();
            addedCount.Should().Be(1);

            // Remove
            state.RemoveComponent<PositionComponent>(e1);
            reactive.Tick();
            removedCount.Should().Be(1);

            // Add again (Reuse)
            state.AddComponent(e1, new PositionComponent());
            reactive.Tick();
            addedCount.Should().Be(2);
        }

        [Fact]
        public void Dispose_ShouldStopNotifications()
        {
            var state = new EntityWorld();
            state.RegisterComponent<PositionComponent>();
            var reactive = new ReactiveSystem();
            
            var addedCount = 0;
            var sub = reactive.ObserveArchetype<PositionComponent>(
                state,
                onAdd: (_) => addedCount++,
                onRemove: (_) => { });

            var e1 = state.CreateEntity();
            state.AddComponent(e1, new PositionComponent());
            reactive.Tick();
            addedCount.Should().Be(1);

            sub.Dispose();

            var e2 = state.CreateEntity();
            state.AddComponent(e2, new PositionComponent());
            reactive.Tick();
            
            addedCount.Should().Be(1); // Should not increase
        }

        [Fact]
        public void ObserveArchetype_FluentApi_ShouldFilterCorrectly()
        {
            var state = new EntityWorld();
            state.RegisterComponent<PositionComponent>();
            
            var dispatcher = new Dispatcher();
            var scheduler = new ActionScheduler();
            var simulation = new GameSimulation(state, dispatcher, scheduler);
            var gameLoop = new GameLoop(simulation);
            
            var reactive = new ReactiveSystem();
            reactive.Bind(state, gameLoop); // Bind state for fluent API

            var addedCount = 0;
            var removedCount = 0;
            var lastAdded = -1;

            // Fluent API usage
            reactive.ObservableCollection<PositionComponent>()
                .Where<PositionComponent>(h => h.X > 50)
                .Subscribe(
                    onAdd: (e) => { addedCount++; lastAdded = e.Id; },
                    onRemove: (e) => { removedCount++; }
                );

            // 1. Add entity with X 100 (Should Match)
            var e1 = state.CreateEntity();
            state.AddComponent(e1, new PositionComponent { X = 100 });
            reactive.Tick();

            addedCount.Should().Be(1);
            lastAdded.Should().Be(e1.Id);

            // 2. Add entity with X 10 (Should NOT Match)
            var e2 = state.CreateEntity();
            state.AddComponent(e2, new PositionComponent { X = 10 });
            reactive.Tick();

            addedCount.Should().Be(1); // No change

            // 3. Update e2 to X 60 (Should Match NOW)
            // Note: EntityWorld.AddComponent calls MarkDirty
            state.AddComponent(e2, new PositionComponent { X = 60 }); 
            reactive.Tick();

            addedCount.Should().Be(2);
            lastAdded.Should().Be(e2.Id);

            // 4. Update e1 to X 0 (Should Unmatch)
            state.AddComponent(e1, new PositionComponent { X = 0 });
            reactive.Tick();

            removedCount.Should().Be(1);
        }
        [Fact]
        public void ArchetypeObserver_ShouldExpandCapacity_WhenMoreThan256EntitiesAdded()
        {
            var state = new EntityWorld();
            var reactive = new ReactiveSystem();
            
            var addedCount = 0;
            reactive.ObserveArchetype<PositionComponent>(state, _ => addedCount++, _ => { });

            // Default capacity is 256. Add 300 entities.
            for (int i = 0; i < 300; i++)
            {
                var e = state.CreateEntity();
                state.AddComponent(e, new PositionComponent());
            }

            reactive.Tick();
            addedCount.Should().Be(300);
        }

        [Fact]
        public void ArchetypeObserver_ShouldHandleGhostEntities_InFullScan()
        {
            var state = new EntityWorld();
            var reactive = new ReactiveSystem();
            var removedCount = 0;

            // 1. Setup observer
            var observer = reactive.ObserveArchetype<PositionComponent>(state, _ => { }, _ => removedCount++) as ArchetypeObserver;
            
            // 2. Add entities
            var e1 = state.CreateEntity();
            state.AddComponent(e1, new PositionComponent());
            reactive.Tick(); // Processes add

            // 3. Create a future entity manually
            var e2 = state.CreateEntity();
            state.AddComponent(e2, new PositionComponent());
            reactive.Tick(); 
            
            var dispatcher = new Dispatcher();
            var scheduler = new ActionScheduler();
            var simulation = new GameSimulation(state, dispatcher, scheduler);
            var loop = new GameLoop(simulation);
            reactive.Bind(state, loop);

            // Tick 1 (Stores state at Tick 1)
            loop.RunSingleTick(); 
            
            // Tick 2: Add entity e3 (ID 2)
            var e3 = state.CreateEntity();
            state.AddComponent(e3, new PositionComponent());
            loop.RunSingleTick(); // Observer sees ID 2. (Stores state at Tick 2)

            // Tick 3: Advance to ensure we can rollback to Tick 2
            loop.RunSingleTick(); // Stores state at Tick 3.
            
            var removedList = new List<int>();
            reactive.ObserveArchetype<PositionComponent>(state, _ => { }, e => removedList.Add(e.Id));
            
            // Schedule rollback to Tick 2 (Input at 2).
            // This triggers restore to Tick 1 (dirtyTick - 1).
            // History has Tick 1.
            var e4 = state.CreateEntity();
            simulation.ScheduleOnTick(2, new TestRollbackAction(), e4);
             
            // Run tick.
            // Restore Tick 1 (Entities: e1, e2. e3 is gone).
            // Resimulate 1 -> 2. e3 is NOT recreated (manual add).
            // Normal Tick 2 -> 3... wait, we were at 3.
            // Restore 1. Resim 1->2->3.
            // Sim 4.
            // Observer Reset() -> FullScan().
            // Should see e3 (ID 2) in bitmask but not in state.
            // Should remove e3.
             
            loop.RunSingleTick();
             
            removedList.Should().Contain(e3.Id);
        }

        [Fact]
        public void ArchetypeObserver_ShouldDetectRemoval_Simple()
        {
            var state = new EntityWorld();
            var reactive = new ReactiveSystem();
            state.RegisterComponent<PositionComponent>();
            state.RegisterComponent<TagComponent>();

            var removed = new List<int>();
            
            // 1. Observe
            reactive.ObserveArchetype<PositionComponent, TagComponent>(state, _ => { }, e => removed.Add(e.Id));
            
            // 2. Add Entity
            var e1 = state.CreateEntity();
            state.AddComponent(e1, new PositionComponent());
            state.AddComponent(e1, new TagComponent());
            
            // 3. Tick (Add detected)
            reactive.Tick();
            
            // 4. Remove Component
            state.RemoveComponent<TagComponent>(e1);
            
            // 5. Tick (Remove detected)
            reactive.Tick();
            
            removed.Should().ContainSingle().Which.Should().Be(e1.Id);
        }

        [Fact]
        public void ArchetypeObserver_ShouldNotCrash_WhenCallbacksAreNull()
        {
            var state = new EntityWorld();
            var reactive = new ReactiveSystem();
            
            var observer = new ArchetypeObserver(); 
            var mask = new BitMask128();
            mask.Set(ComponentId<PositionComponent>.IntId);
            
            observer.Initialize(state, mask, null!, null!);
            reactive.Register(observer);
            
            var e = state.CreateEntity();
            state.AddComponent(e, new PositionComponent());
            
            Action act = () => reactive.Tick();
            act.Should().NotThrow();
            
            state.RemoveComponent<PositionComponent>(e);
            act.Should().NotThrow();
        }

        [Fact]
        public void ArchetypeObserver_ResimulationGuard_Test()
        {
             var state = new EntityWorld();
             var dispatcher = new Dispatcher();
             var scheduler = new ActionScheduler();
             var simulation = new GameSimulation(state, dispatcher, scheduler);
             var loop = new GameLoop(simulation);
             var reactive = new ReactiveSystem();
             reactive.Bind(state, loop);
             
             var callbackFiredCount = 0;
             var observer = new ArchetypeObserver();
             var mask = new BitMask128();
             mask.Set(ComponentId<PositionComponent>.IntId);
             observer.Initialize(state, mask, _ => callbackFiredCount++, null!);
             reactive.Register(observer);
             
             var resimSystem = new ResimulationTriggerSystem(observer, () => callbackFiredCount);
             simulation.SystemRunner.EnableSystem(resimSystem);

             // Build History: Tick 1, 2, 3
             loop.RunSingleTick(); 
             loop.RunSingleTick(); 
             loop.RunSingleTick(); 
             
             // CurrentTick is 3.
             // Schedule at 2. Dirty=2. < 3.
             // Restore = 2 - 1 = 1. History has 1.
             
             var e = state.CreateEntity();
             simulation.ScheduleOnTick(2, new TestRollbackAction(), e);
             
             var e2 = state.CreateEntity();
             state.AddComponent(e2, new PositionComponent());
             
             loop.RunSingleTick();
             
             resimSystem.DidRun.Should().BeTrue();
             resimSystem.CountBefore.Should().Be(resimSystem.CountAfter, "Callback should not fire during resimulation even if CheckAndNotify is called");
        }
        
        [Fact]
        public void ArchetypeObserver_ManualCheckAndNotify_DuringResimulation_ShouldReturnEarly()
        {
             var state = new EntityWorld();
             var dispatcher = new Dispatcher();
             var scheduler = new ActionScheduler();
             var simulation = new GameSimulation(state, dispatcher, scheduler);
             var loop = new GameLoop(simulation);
             var reactive = new ReactiveSystem();
             reactive.Bind(state, loop);
             
             var observer = new ArchetypeObserver();
             var mask = new BitMask128();
             mask.Set(ComponentId<PositionComponent>.IntId);
             observer.Initialize(state, mask, _ => { }, null!);
             reactive.Register(observer); 
             
             var testSystem = new ManualTriggerSystem(observer);
             simulation.SystemRunner.EnableSystem(testSystem);
             
             // Build History
             loop.RunSingleTick(); 
             loop.RunSingleTick(); 
             loop.RunSingleTick(); 
             
             var e = state.CreateEntity();
             simulation.ScheduleOnTick(2, new TestRollbackAction(), e);
             
             var e2 = state.CreateEntity();
             state.AddComponent(e2, new PositionComponent());
             
             loop.RunSingleTick();
             
             testSystem.WasCalledDuringResim.Should().BeTrue("System should have run during resim");
             testSystem.ObserverTriggeredDuringResim.Should().BeFalse("Observer should ignore CheckAndNotify during resim");
        }

        [Fact]
        public void ArchetypeObserver_ShouldRemoveGhostEntities_OnReset()
        {
            // 1. Setup World
            var state = new EntityWorld();
            var reactive = new ReactiveSystem();
            
            var added = new List<int>();
            var removed = new List<int>();
            
            // Cast to ObserverNode to access the public interface for testing if needed, 
            // but ObserveArchetype returns IDisposable. We cast to ObserverNode to call Reset manually.
            var observer = (ObserverNode)reactive.ObserveArchetype<PositionComponent>(
                state,
                e => added.Add(e.Id),
                e => removed.Add(e.Id)
            );

            // 2. Create entities (Tick 1)
            var e1 = state.CreateEntity();
            state.AddComponent(e1, new PositionComponent());
            
            reactive.Tick();
            added.Should().Contain(e1.Id);
            added.Clear();

            // 3. Save State (Snapshot at Tick 1)
            var snapshot = StateSerializer.Serialize(state);

            // 4. Create "Future" entities (Tick 2)
            var e2 = state.CreateEntity(); // This will be a ghost after rollback
            state.AddComponent(e2, new PositionComponent());
            
            reactive.Tick();
            added.Should().Contain(e2.Id);
            added.Clear();

            // 5. Rollback (Restore Tick 1)
            // This resets NextEntityId to what it was at snapshot
            StateSerializer.Deserialize(state, snapshot, syncComponentIds: false);
            
            // At this point, e2 is gone from state, but Observer still thinks it matches.
            // Observer internal bitset has e2 set.
            // state.NextEntityId is back to (e1.Id + 1).
            
            // 6. Trigger Reset
            // ReactiveSystem usually calls this on rollback completion.
            // We can manually call Reset on the observer for this test since we casted it.
            observer.Reset();

            // 7. Verify e2 is removed
            removed.Should().Contain(e2.Id, "Entity 2 should be removed as it is now a ghost entity beyond NextEntityId");
            
            // Verify e1 is NOT removed
            removed.Should().NotContain(e1.Id);
        }

        /// <summary>
        /// Reproduces the "disappearing props" bug after a state desync in a networked game.
        ///
        /// Timeline:
        ///   1. Entities exist with PositionComponent+TagComponent. Observer fires onAdd.
        ///   2. Tick completes. ClearDirty() wipes the dirty list.
        ///   3. Desync: RemoveComponent strips TagComponent from some entities (simulating
        ///      entity-ID swap where entity 5 is Grass on client but Player on server).
        ///      MarkDirty fires. Observer processes removes via CheckAndNotify.
        ///   4. Tick completes. ClearDirty() wipes the dirty list again.
        ///   5. State correction: the server state arrives and we restore the entity masks
        ///      directly (bypassing AddComponent, so MarkDirty is NOT called).
        ///      This simulates what happens during state sync / deserialization when
        ///      MarkAllDirty is not called afterwards.
        ///   6. More ticks run. Observer never re-checks these entities because they are
        ///      not in the dirty list.
        ///   7. BUG: The entities are gone from the observer's matched set (removed in
        ///      step 3) and never re-added. Visual props disappear permanently.
        ///
        /// This test should FAIL with the current code, confirming the bug.
        /// The fix would be to either:
        ///   a) Always call MarkAllDirty() after state correction, or
        ///   b) Use IsPaused during the desync window so removes never fire, and
        ///      Reset() reconciles on unpause.
        /// </summary>
        [Fact]
        public void ArchetypeObserver_DesyncCorrection_EntitiesShouldReappear_WhenMasksRestoredDirectly()
        {
            // -- Arrange --
            var state = new EntityWorld();
            state.RegisterComponent<PositionComponent>();
            state.RegisterComponent<TagComponent>();
            var reactive = new ReactiveSystem();

            var added = new List<int>();
            var removed = new List<int>();

            reactive.ObserveArchetype<PositionComponent, TagComponent>(
                state,
                e => added.Add(e.Id),
                e => removed.Add(e.Id));

            // Create 5 "prop" entities with both components
            var entities = new Entity[5];
            for (int i = 0; i < 5; i++)
            {
                entities[i] = state.CreateEntity();
                state.AddComponent(entities[i], new PositionComponent { X = i * 10 });
                state.AddComponent(entities[i], new TagComponent { TagId = i });
            }

            // Step 1: First tick - observer detects all 5 entities
            reactive.Tick();
            added.Count.Should().Be(5, "all 5 entities should be detected on first tick");
            removed.Count.Should().Be(0);

            // Step 2: Clear dirty (simulates end-of-tick in GameSimulation)
            state.ClearDirty();
            added.Clear();
            removed.Clear();

            // Step 3: DESYNC - remove TagComponent from entities 1,2,3
            // (simulating entity-ID swap: these entities temporarily look like
            //  Player entities instead of Grass/Prop entities)
            state.RemoveComponent<TagComponent>(entities[1]);
            state.RemoveComponent<TagComponent>(entities[2]);
            state.RemoveComponent<TagComponent>(entities[3]);

            // Observer fires removes for entities 1,2,3
            reactive.Tick();
            removed.Count.Should().Be(3, "3 entities should be removed during desync");
            added.Count.Should().Be(0);

            // Step 4: Clear dirty (end of desync tick)
            state.ClearDirty();
            added.Clear();
            removed.Clear();

            // Step 5: STATE CORRECTION - restore entity masks directly
            // This simulates what happens when the server sends the correct state
            // and the client restores entity masks via direct assignment (as in
            // StateSerializer.Deserialize or manual mask restoration).
            // Crucially, this does NOT go through AddComponent, so MarkDirty is NOT called.
            var tagTypeId = ComponentId<TagComponent>.IntId;
            state.EntityMasks[entities[1].Id].Set(tagTypeId);
            state.EntityMasks[entities[2].Id].Set(tagTypeId);
            state.EntityMasks[entities[3].Id].Set(tagTypeId);

            // Step 6: Run several more ticks (simulating ~60 ticks of normal play)
            for (int i = 0; i < 10; i++)
            {
                reactive.Tick();
                state.ClearDirty();
            }

            // Step 7: VERIFY - entities 1,2,3 should have been re-added
            // BUG: They are NOT re-added because they were never in the dirty list
            // after the mask restoration. The observer's internal bitset still has
            // them marked as "not matched" from step 3, and CheckAndNotify only
            // checks dirty entities.
            added.Count.Should().Be(3,
                "entities 1,2,3 should be re-added after state correction restores their masks, " +
                "but they are lost because direct mask writes don't trigger MarkDirty");
        }

        /// <summary>
        /// Same desync scenario, but uses IsPaused to suppress callbacks during the
        /// desync window. When unpaused, Reset() fires which does a FullScan.
        /// This is the WORKAROUND that should pass -- proves IsPaused+Reset works.
        /// </summary>
        [Fact]
        public void ArchetypeObserver_DesyncWithPause_EntitiesShouldReappear_AfterUnpause()
        {
            // -- Arrange --
            var state = new EntityWorld();
            state.RegisterComponent<PositionComponent>();
            state.RegisterComponent<TagComponent>();
            var reactive = new ReactiveSystem();

            var added = new List<int>();
            var removed = new List<int>();

            reactive.ObserveArchetype<PositionComponent, TagComponent>(
                state,
                e => added.Add(e.Id),
                e => removed.Add(e.Id));

            // Create 5 "prop" entities
            var entities = new Entity[5];
            for (int i = 0; i < 5; i++)
            {
                entities[i] = state.CreateEntity();
                state.AddComponent(entities[i], new PositionComponent { X = i * 10 });
                state.AddComponent(entities[i], new TagComponent { TagId = i });
            }

            // First tick - all 5 detected
            reactive.Tick();
            added.Count.Should().Be(5);
            state.ClearDirty();
            added.Clear();
            removed.Clear();

            // PAUSE before desync (client detects mismatch, pauses reactive system)
            reactive.IsPaused = true;

            // DESYNC - remove TagComponent from entities 1,2,3
            state.RemoveComponent<TagComponent>(entities[1]);
            state.RemoveComponent<TagComponent>(entities[2]);
            state.RemoveComponent<TagComponent>(entities[3]);

            // Tick while paused - should NOT fire any callbacks
            reactive.Tick();
            removed.Count.Should().Be(0, "no removes should fire while paused");
            state.ClearDirty();

            // STATE CORRECTION - restore masks directly
            var tagTypeId = ComponentId<TagComponent>.IntId;
            state.EntityMasks[entities[1].Id].Set(tagTypeId);
            state.EntityMasks[entities[2].Id].Set(tagTypeId);
            state.EntityMasks[entities[3].Id].Set(tagTypeId);

            // UNPAUSE - this triggers Reset() which does FullScan
            reactive.IsPaused = false;

            // All 5 entities still have both components, observer should have them all matched.
            // Since entities 1,2,3 were removed from observer's bitset during the mask change
            // but Reset() re-scans everything, they should be re-added.
            // Actually: since IsPaused suppressed the tick, the observer's bitset still has
            // entities 1,2,3 as matched. And after mask restoration + Reset(), they still match.
            // So no removes or adds should have fired -- net zero change.
            // The key insight: IsPaused prevents the observer from ever seeing the intermediate
            // bad state, so no visual flicker occurs.
            removed.Count.Should().Be(0, "no removes should fire when paused during desync");

            // Run normal ticks - all 5 should still be tracked
            for (int i = 0; i < 5; i++)
            {
                reactive.Tick();
                state.ClearDirty();
            }

            // Verify by removing all entities - should get 5 removes
            removed.Clear();
            for (int i = 0; i < 5; i++)
            {
                state.RemoveComponent<TagComponent>(entities[i]);
            }
            reactive.Tick();
            removed.Count.Should().Be(5,
                "all 5 entities should still be tracked after desync+pause+unpause cycle");
        }

        /// <summary>
        /// Variant: desync where RemoveComponent is used (marks dirty), then state
        /// correction also uses AddComponent (marks dirty). This SHOULD work because
        /// both paths go through MarkDirty. Included as a baseline/control test.
        /// </summary>
        [Fact]
        public void ArchetypeObserver_DesyncCorrection_ViaAddComponent_ShouldWork()
        {
            var state = new EntityWorld();
            state.RegisterComponent<PositionComponent>();
            state.RegisterComponent<TagComponent>();
            var reactive = new ReactiveSystem();

            var added = new List<int>();
            var removed = new List<int>();

            reactive.ObserveArchetype<PositionComponent, TagComponent>(
                state,
                e => added.Add(e.Id),
                e => removed.Add(e.Id));

            var entities = new Entity[5];
            for (int i = 0; i < 5; i++)
            {
                entities[i] = state.CreateEntity();
                state.AddComponent(entities[i], new PositionComponent { X = i * 10 });
                state.AddComponent(entities[i], new TagComponent { TagId = i });
            }

            reactive.Tick();
            added.Count.Should().Be(5);
            state.ClearDirty();
            added.Clear();
            removed.Clear();

            // DESYNC - remove via RemoveComponent (marks dirty)
            state.RemoveComponent<TagComponent>(entities[1]);
            state.RemoveComponent<TagComponent>(entities[2]);
            state.RemoveComponent<TagComponent>(entities[3]);
            reactive.Tick();
            removed.Count.Should().Be(3);
            state.ClearDirty();
            added.Clear();
            removed.Clear();

            // STATE CORRECTION - restore via AddComponent (marks dirty!)
            state.AddComponent(entities[1], new TagComponent { TagId = 1 });
            state.AddComponent(entities[2], new TagComponent { TagId = 2 });
            state.AddComponent(entities[3], new TagComponent { TagId = 3 });
            reactive.Tick();

            // This SHOULD work because AddComponent calls MarkDirty
            added.Count.Should().Be(3,
                "entities restored via AddComponent should be re-added (control test)");
        }

        [Fact]
        public void ArchetypeObserver_ShouldHandleCapacityExpansion_DuringReset()
        {
            var state = new EntityWorld();
            var reactive = new ReactiveSystem();
            var observer = (ObserverNode)reactive.ObserveArchetype<PositionComponent>(
                state, _ => {}, _ => {});

            // Create enough entities to force capacity expansion if we were tracking them
            int count = 300; // > 256 default
            for(int i=0; i<count; i++)
            {
                state.CreateEntity();
            }
            
            // Reset should expand internal arrays without crashing
            Action act = () => observer.Reset();
            act.Should().NotThrow();
        }

        [Fact]
        public void ArchetypeObserver_CheckAndNotify_ShouldNoOp_WhenOwnerNull()
        {
            // Covers ArchetypeObserver.cs Line 53 (Owner null case)
            var state = new EntityWorld();
            var observer = new ArchetypeObserver();
            // Initialize so _state is not null
            observer.Initialize(state, new BitMask128(), _ => { }, _ => { });
            
            // Not registered, so Owner is null.
            
            Action act = () => observer.CheckAndNotify();
            act.Should().NotThrow();
        }

        [Fact]
        public void ArchetypeObserver_Reset_ShouldHandleNullOnRemove_WithGhostEntities()
        {
            // Covers ArchetypeObserver.cs Line 105 (Ghost removal with null callback)
            var state = new EntityWorld();
            var reactive = new ReactiveSystem();
            
            var observer = new ArchetypeObserver();
            var mask = new BitMask128();
            mask.Set(ComponentId<PositionComponent>.IntId);
            
            // Initialize with NULL callbacks
            observer.Initialize(state, mask, null!, null!);
            reactive.Register(observer);
            
            // 1. Add entity
            var e = state.CreateEntity();
            state.AddComponent(e, new PositionComponent());
            
            // 2. Process Add (sets bit in observer)
            reactive.Tick();
            
            // 3. Manually simulate rollback state (reset NextEntityId)
            // We can't easily change NextEntityId directly as it is internal/private set usually,
            // but we can use StateSerializer to restore an empty state.
            var emptyState = new EntityWorld();
            var snapshot = StateSerializer.Serialize(emptyState);
            StateSerializer.Deserialize(state, snapshot, syncComponentIds: false);
            
            // Now state.NextEntityId should be small (0 or 1), but observer has bit set for 'e'.
            
            // 4. Call Reset
            Action act = () => observer.Reset();
            act.Should().NotThrow();
        }
        
        private class ManualTriggerSystem : ISystem
        {
            private readonly ArchetypeObserver _observer;
            public bool WasCalledDuringResim;
            public bool ObserverTriggeredDuringResim = false;

            public ManualTriggerSystem(ArchetypeObserver observer)
            {
                _observer = observer;
            }

            public void Update(EntityWorld world)
            {
                if (_observer.Owner!.IsResimulating)
                {
                    WasCalledDuringResim = true;
                    _observer.CheckAndNotify();
                }
            }
        }
        
        private class ResimulationTriggerSystem : ISystem
        {
            private readonly ArchetypeObserver _observer;
            private readonly Func<int> _countProvider;
            public bool DidRun;
            public int CountBefore;
            public int CountAfter;

            public ResimulationTriggerSystem(ArchetypeObserver observer, Func<int> countProvider)
            {
                _observer = observer;
                _countProvider = countProvider;
            }

            public void Update(EntityWorld world)
            {
                if (_observer.Owner!.IsResimulating)
                {
                    DidRun = true;
                    CountBefore = _countProvider();
                    _observer.CheckAndNotify();
                    CountAfter = _countProvider();
                }
            }
        }
        [Fact]
        public void ObserveArchetype_T1_T2_T3_ShouldMatchCorrectly()
        {
            var state = new EntityWorld();
            state.RegisterComponent<PositionComponent>();
            state.RegisterComponent<VelocityComponent>();
            state.RegisterComponent<TagComponent>();

            var reactive = new ReactiveSystem();
            var added = new List<Entity>();
            var removed = new List<Entity>();

            reactive.ObserveArchetype<PositionComponent, VelocityComponent, TagComponent>(
                state,
                e => added.Add(e),
                e => removed.Add(e)
            );

            // Match
            var e1 = state.CreateEntity();
            state.AddComponent(e1, new PositionComponent());
            state.AddComponent(e1, new VelocityComponent());
            state.AddComponent(e1, new TagComponent());

            // Partial match (no tag)
            var e2 = state.CreateEntity();
            state.AddComponent(e2, new PositionComponent());
            state.AddComponent(e2, new VelocityComponent());

            reactive.Tick();

            added.Should().ContainSingle().Which.Id.Should().Be(e1.Id);

            // Remove component to trigger remove
            state.RemoveComponent<TagComponent>(e1);
            reactive.Tick();

            removed.Should().ContainSingle().Which.Id.Should().Be(e1.Id);
        }
        [Fact]
        public void ObserveArchetype_ShouldReuse_Observer()
        {
            var state = new EntityWorld();
            var reactive = new ReactiveSystem();
            
            var e1 = state.CreateEntity();
            var addedCount = 0;
            var removedCount = 0;

            var observer = (ObserverNode)reactive.ObserveArchetype<PositionComponent>(
                state,
                onAdd: (e) => {
                    addedCount++;
                },
                onRemove: (e) => {
                    removedCount++;
                });
                
            // Add
            state.AddComponent(e1, new PositionComponent());
            reactive.Tick();
            
            // Remove
            state.RemoveComponent<PositionComponent>(e1);
            reactive.Tick();

            // Add again (Reuse)
            state.AddComponent(e1, new PositionComponent());
            reactive.Tick();
            
            addedCount.Should().Be(2);
            removedCount.Should().Be(1);
        }
    }
}
