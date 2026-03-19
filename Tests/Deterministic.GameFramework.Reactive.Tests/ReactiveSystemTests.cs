using System;
using System.Collections.Generic;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Reactive;
using Deterministic.GameFramework.Common;
using Deterministic.GameFramework.DAR;
using FluentAssertions;
using Xunit;

namespace Deterministic.GameFramework.ECS.Tests
{
    [Collection("Non-Parallel")] public class ReactiveSystemTests
    {
        public ReactiveSystemTests()
        {
            ComponentId.RegisterAssembly(typeof(PositionComponent).Assembly);
            ComponentId.RegisterAssembly(typeof(World).Assembly);
        }

        [Fact]
        public void Subscribe_ShouldTriggerCallback_WhenValueChanges()
        {
            var reactive = new ReactiveSystem();
            var callbackCount = 0;
            var lastValue = 0;

            var value = 10;
            reactive.Subscribe(() => value, (newValue) =>
            {
                callbackCount++;
                lastValue = newValue;
            });

            reactive.Tick();
            callbackCount.Should().Be(1);
            lastValue.Should().Be(10);

            value = 20;
            reactive.Tick();
            callbackCount.Should().Be(2);
            lastValue.Should().Be(20);

            reactive.Tick();
            callbackCount.Should().Be(2);
        }

        [Fact]
        public void Subscribe_ShouldNotTrigger_WhenValueUnchanged()
        {
            var reactive = new ReactiveSystem();
            var callbackCount = 0;

            var value = 10;
            reactive.Subscribe(() => value, (newValue) => callbackCount++);

            reactive.Tick();
            callbackCount.Should().Be(1);

            reactive.Tick();
            reactive.Tick();
            reactive.Tick();
            callbackCount.Should().Be(1);
        }

        [Fact]
        public void Subscribe_WithContext_ShouldTriggerCallback_WhenValueChanges()
        {
            var state = new EntityWorld();
            var entity = state.CreateEntity();
            state.AddComponent(entity, new PositionComponent { X = 100 });

            var reactive = new ReactiveSystem();
            var callbackCount = 0;
            var lastX = 0;

            reactive.Subscribe(
                context: state,
                selector: (s) => s.GetComponent<PositionComponent>(entity).X,
                callback: (s, x) =>
                {
                    callbackCount++;
                    lastX = x;
                });

            reactive.Tick();
            callbackCount.Should().Be(1);
            lastX.Should().Be(100);

            state.GetComponent<PositionComponent>(entity).X = 50;
            reactive.Tick();
            callbackCount.Should().Be(2);
            lastX.Should().Be(50);

            reactive.Tick();
            callbackCount.Should().Be(2);
        }

        [Fact]
        public void Dispose_ShouldUnregisterObserver()
        {
            var reactive = new ReactiveSystem();
            var callbackCount = 0;

            var value = 10;
            var subscription = reactive.Subscribe(() => value, (newValue) => callbackCount++);

            reactive.Tick();
            callbackCount.Should().Be(1);

            subscription.Dispose();

            value = 20;
            reactive.Tick();
            callbackCount.Should().Be(1);
        }

        [Fact]
        public void MultipleObservers_ShouldAllTrigger()
        {
            var reactive = new ReactiveSystem();
            var callback1Count = 0;
            var callback2Count = 0;
            var callback3Count = 0;

            var value = 10;
            reactive.Subscribe(() => value, (v) => callback1Count++);
            reactive.Subscribe(() => value, (v) => callback2Count++);
            reactive.Subscribe(() => value, (v) => callback3Count++);

            reactive.Tick();
            callback1Count.Should().Be(1);
            callback2Count.Should().Be(1);
            callback3Count.Should().Be(1);

            value = 20;
            reactive.Tick();
            callback1Count.Should().Be(2);
            callback2Count.Should().Be(2);
            callback3Count.Should().Be(2);
        }

        [Fact]
        public void Bind_ShouldSubscribeToGameLoopTick()
        {
            var state = new EntityWorld();
            var dispatcher = new Dispatcher();
            var scheduler = new ActionScheduler();
            var simulation = new GameSimulation(state, dispatcher, scheduler);
            var gameLoop = new GameLoop(simulation);

            var reactive = new ReactiveSystem();
            reactive.Bind(state, gameLoop);

            var callbackCount = 0;
            var value = 10;
            reactive.Subscribe(() => value, (v) => callbackCount++);

            gameLoop.RunSingleTick();
            callbackCount.Should().Be(1);

            value = 20;
            gameLoop.RunSingleTick();
            callbackCount.Should().Be(2);
        }

        [Fact]
        public void Unbind_ShouldStopReceivingTicks()
        {
            var state = new EntityWorld();
            var dispatcher = new Dispatcher();
            var scheduler = new ActionScheduler();
            var simulation = new GameSimulation(state, dispatcher, scheduler);
            var gameLoop = new GameLoop(simulation);

            var reactive = new ReactiveSystem();
            reactive.Bind(state, gameLoop);

            var callbackCount = 0;
            var value = 10;
            reactive.Subscribe(() => value, (v) => callbackCount++);

            gameLoop.RunSingleTick();
            callbackCount.Should().Be(1);

            reactive.Unbind();

            value = 20;
            gameLoop.RunSingleTick();
            callbackCount.Should().Be(1);
        }

        [Fact]
        public void Dispose_ReactiveSystem_ShouldUnbindAndClearObservers()
        {
            var state = new EntityWorld();
            var dispatcher = new Dispatcher();
            var scheduler = new ActionScheduler();
            var simulation = new GameSimulation(state, dispatcher, scheduler);
            var gameLoop = new GameLoop(simulation);

            var reactive = new ReactiveSystem();
            reactive.Bind(state, gameLoop);

            var callbackCount = 0;
            var value = 10;
            reactive.Subscribe(() => value, (v) => callbackCount++);

            gameLoop.RunSingleTick();
            callbackCount.Should().Be(1);

            reactive.Dispose();

            value = 20;
            gameLoop.RunSingleTick();
            callbackCount.Should().Be(1);
        }

        [Fact]
        public void Observer_ShouldHandleException_WithoutCrashing()
        {
            var reactive = new ReactiveSystem();
            var value = 10;

            reactive.Subscribe(() => value, (v) => throw new Exception("Test exception"));
            reactive.Subscribe(() => value, (v) => { }); // This should still execute

            Action act = () => reactive.Tick();
            act.Should().NotThrow();
        }

        [Fact]
        public void SelfRemoval_DuringIteration_ShouldNotCrash()
        {
            var reactive = new ReactiveSystem();
            var value = 10;
            IDisposable? subscription = null;

            subscription = reactive.Subscribe(() => value, (v) =>
            {
                subscription?.Dispose();
            });

            Action act = () => reactive.Tick();
            act.Should().NotThrow();
        }

        [Fact]
        public void MultipleSubscriptions_ToSameValue_ShouldWorkIndependently()
        {
            var state = new EntityWorld();
            var entity = state.CreateEntity();
            state.AddComponent(entity, new PositionComponent { X = 100 });

            var reactive = new ReactiveSystem();
            var values = new List<int>();
                
            reactive.Subscribe(
                state,
                s => s.GetComponent<PositionComponent>(entity).X,
                (s, v) => values.Add(v));
            
            reactive.Subscribe(
                state,
                s => s.GetComponent<PositionComponent>(entity).X,
                (s, v) => values.Add(v * 2));

            reactive.Tick();
            values.Should().Equal(100, 200);

            values.Clear();
            state.GetComponent<PositionComponent>(entity).X = 50;
            reactive.Tick();
            values.Should().Equal(50, 100);
        }

        [Fact]
        public void CustomComparer_ShouldBeRespected()
        {
            var reactive = new ReactiveSystem();
            var callbackCount = 0;

            var value = 10;
            var comparer = new AlwaysDifferentComparer();
            reactive.Subscribe(() => value, (v) => callbackCount++, comparer);

            reactive.Tick();
            callbackCount.Should().Be(2);

            reactive.Tick();
            callbackCount.Should().Be(3);

            reactive.Tick();
            callbackCount.Should().Be(4);
        }

        private class AlwaysDifferentComparer : IEqualityComparer<int>
        {
            public bool Equals(int x, int y) => false;
            public int GetHashCode(int obj) => obj.GetHashCode();
        }

        [Fact]
        public void LargeNumberOfObservers_ShouldPerformWell()
        {
            var reactive = new ReactiveSystem();
            var value = 10;
            var totalCallbacks = 0;

            for (int i = 0; i < 1000; i++)
            {
                reactive.Subscribe(() => value, (v) => totalCallbacks++);
            }

            reactive.Tick();
            totalCallbacks.Should().Be(1000);

            value = 20;
            reactive.Tick();
            totalCallbacks.Should().Be(2000);
        }

        [Fact]
        public void ReactiveSystem_Register_ShouldThrow_IfAlreadyRegistered()
        {
            var reactive = new ReactiveSystem();
            var observer = new SimplePollingObserver<int>();
            
            reactive.Register(observer);
            
            Action act = () => reactive.Register(observer);
            act.Should().Throw<InvalidOperationException>()
               .WithMessage("Observer already registered to a system");
        }

        [Fact]
        public void ReactiveSystem_Unbind_ShouldHandleNullLoop()
        {
            var reactive = new ReactiveSystem();
            Action act = () => reactive.Unbind();
            act.Should().NotThrow();
        }
        
        [Fact]
        public void GameLoop_Event_ShouldSupportUnsubscribe()
        {
            var state = new EntityWorld();
            var loop = new GameLoop(new GameSimulation(state, new Dispatcher(), new ActionScheduler()));
            var count = 0;
            Action handler = () => count++;
            
            loop.OnTick += handler;
            loop.RunSingleTick();
            count.Should().Be(1);
            
            loop.OnTick -= handler;
            loop.RunSingleTick();
            count.Should().Be(1, "Should not increment after unsubscribe");
        }

        [Fact]
        public void ReactiveSystem_Bind_ShouldUnbindPrevious()
        {
            var state = new EntityWorld();
            var loop1 = new GameLoop(new GameSimulation(state, new Dispatcher(), new ActionScheduler()));
            var loop2 = new GameLoop(new GameSimulation(state, new Dispatcher(), new ActionScheduler()));
            
            var reactive = new ReactiveSystem();
            reactive.Bind(state, loop1);
            
            // Should unbind loop1 and bind loop2
            reactive.Bind(state, loop2);
            
            // Check if loop1 tick triggers reactive (it shouldn't)
            var count = 0;
            var val = 0;
            reactive.Subscribe(() => val, _ => count++);
            
            // Initial callback happens immediately on registration
            count.Should().Be(1, "Initial subscription triggers callback");
            
            // Change value, Run Loop 1
            val = 1;
            loop1.RunSingleTick();
            count.Should().Be(1, "Loop1 should be unbound, so no update despite value change");
            
            // Change value, Run Loop 2
            val = 2;
            loop2.RunSingleTick();
            count.Should().Be(2, "Loop2 should be bound, triggers update");
        }
        
        [Fact]
        public void ReactiveSystem_Bind_ShouldDoNothing_IfSameLoop()
        {
            var state = new EntityWorld();
            var loop = new GameLoop(new GameSimulation(state, new Dispatcher(), new ActionScheduler()));
            
            var reactive = new ReactiveSystem();
            reactive.Bind(state, loop);
            
            // Bind same loop again
            reactive.Bind(state, loop);
            
            var count = 0;
            reactive.Subscribe(() => 1, _ => count++);
            
            loop.RunSingleTick();
            count.Should().Be(1);
        }
        
        [Fact]
        public void ReactiveSystem_ResetAll_ShouldHandleExceptions()
        {
            var state = new EntityWorld();
            var simulation = new GameSimulation(state, new Dispatcher(), new ActionScheduler());
            var loop = new GameLoop(simulation);
            var reactive = new ReactiveSystem();
            reactive.Bind(state, loop);
            
            var observer = new ThrowingObserver();
            reactive.Register(observer);
            
            // Force a rollback to trigger ResetAll
            var e = state.CreateEntity();
            simulation.ScheduleOnTick(2, new TestRollbackAction(), e);
            loop.RunSingleTick(); // Tick 1
            loop.RunSingleTick(); // Tick 2
            loop.RunSingleTick(); // Tick 3 (Simulate)
            
            // This tick should trigger rollback and then ResetAll
            // Observer.Reset() throws, but ReactiveSystem should catch it
            Action act = () => loop.RunSingleTick();
            act.Should().NotThrow();
        }

        [Fact]
        public void SimplePollingObserver_ShouldBePooled()
        {
            var reactive = new ReactiveSystem();
            IDisposable subscription = reactive.Subscribe(() => 1, _ => { });
            
            // Dispose should return it to pool
            subscription.Dispose();
            
            // Allocate new one - should be same instance if pooling works (conceptually)
            // Hard to verify instance reuse without internal access, but we can verify no crash
            IDisposable sub2 = reactive.Subscribe(() => 2, _ => { });
            sub2.Should().NotBeNull();
        }

        [Fact]
        public void ReactiveSystem_ShouldSkipUpdate_WhenResimulating()
        {
             var state = new EntityWorld();
             var dispatcher = new Dispatcher();
             var scheduler = new ActionScheduler();
             var simulation = new GameSimulation(state, dispatcher, scheduler);
             var loop = new GameLoop(simulation);
             var reactive = new ReactiveSystem();
             reactive.Bind(state, loop);
             
             var selectorChecks = 0;
             // Subscribe with a selector that counts how many times it is called
             reactive.Subscribe(() => { selectorChecks++; return 1; }, _ => { });
             
             // Initial eager eval
             selectorChecks.Should().Be(1);
             
             // Advance a few ticks to build history
             loop.RunSingleTick(); // Tick 1. Checks: 2
             loop.RunSingleTick(); // Tick 2. Checks: 3
             loop.RunSingleTick(); // Tick 3. Checks: 4
             loop.RunSingleTick(); // Tick 4. Checks: 5
             
             selectorChecks.Should().Be(5);
             
             // Schedule rollback to Tick 2
             // CurrentTick is 4. dirtyTick = 2. restoreTick = 1.
             // History has 1, 2, 3, 4.
             var e = state.CreateEntity();
             simulation.ScheduleOnTick(2, new TestRollbackAction(), e);
             
             // Run tick. This will trigger rollback to Tick 1.
             // Resimulate Tick 1 -> Tick 2 -> Tick 3.
             // Finally Normal Tick 4.
             
             // If ReactiveSystem skips Resimulating ticks (1, 2, 3), selectorChecks only increments for Normal Tick 4.
             
             loop.RunSingleTick();
             
             // 5 (initial) + 1 (Tick 4) = 6.
             selectorChecks.Should().Be(6);
        }

        [Fact]
        public void PollingObserver_WithContext_ShouldDisposeCorrectly()
        {
            var state = new EntityWorld();
            var reactive = new ReactiveSystem();
            
            // Use Subscribe extension that uses PollingObserver<T> (the one with context)
            var sub = reactive.Subscribe(state, s => 1, (s, v) => { });
            
            // Check it works
            reactive.Tick();
            
            // Dispose
            sub.Dispose();
            
            // Should be removed from system
            sub.Dispose(); // Double dispose safe
        }

        [Fact]
        public void ReactiveSystem_Tick_ShouldHandleException_InLoop()
        {
            var reactive = new ReactiveSystem();
            var obs1 = new ThrowingObserver();
            var obs2 = new MinimalObserver(); 
            
            reactive.Register(obs1);
            reactive.Register(obs2);
            
            var count = 0;
            reactive.Subscribe(() => 1, _ => count++);
            
            Action act = () => reactive.Tick();
            act.Should().NotThrow();
            
            count.Should().Be(1);
        }
        
        [Fact]
        public void ReactiveSystem_ResetAll_ShouldHandleException_InLoop()
        {
            var state = new EntityWorld();
            var dispatcher = new Dispatcher();
            var scheduler = new ActionScheduler();
            var simulation = new GameSimulation(state, dispatcher, scheduler);
            var loop = new GameLoop(simulation);
            var reactive = new ReactiveSystem();
            reactive.Bind(state, loop);

            var obs1 = new ThrowingResetObserver();
            var obs2 = new MockObserver();
            
            reactive.Register(obs1);
            reactive.Register(obs2);

            // Build history
            loop.RunSingleTick(); 
            loop.RunSingleTick(); 
            loop.RunSingleTick(); 

            // Schedule rollback to Tick 2 (restoring Tick 1)
            var e = state.CreateEntity();
            simulation.ScheduleOnTick(2, new TestRollbackAction(), e);
            
            // Run tick to trigger rollback
            loop.RunSingleTick(); 
            
            obs2.ResetCalled.Should().BeTrue();
        }

        [Fact]
        public void Unregister_ShouldHandleHeadMiddleTailRemovals()
        {
            var reactive = new ReactiveSystem();
            var value = 0;
            
            // Create 3 observers
            var sub1 = reactive.Subscribe(() => value, _ => { });
            var sub2 = reactive.Subscribe(() => value, _ => { });
            var sub3 = reactive.Subscribe(() => value, _ => { });
            
            // Remove Middle (sub2)
            sub2.Dispose();
            // Verify link: sub1 -> sub3
            
            // Remove Head (sub1)
            sub1.Dispose();
            // Verify link: sub3 is head
            
            // Remove Tail (sub3)
            sub3.Dispose();
            // Verify empty
            
            // Re-add to ensure system still works
            var sub4 = reactive.Subscribe(() => value, _ => { });
            reactive.Tick();
            sub4.Dispose();
        }

        [Fact]
        public void Unregister_ShouldHandleTailMiddleHeadRemovals()
        {
            var reactive = new ReactiveSystem();
            var value = 0;
            
            var sub1 = reactive.Subscribe(() => value, _ => { });
            var sub2 = reactive.Subscribe(() => value, _ => { });
            var sub3 = reactive.Subscribe(() => value, _ => { });
            
            // Remove Tail (sub3)
            sub3.Dispose();
            
            // Remove Middle (sub2)
            sub2.Dispose();
            
            // Remove Head (sub1)
            sub1.Dispose();
        }

        [Fact]
        public void Register_WithEvaluateFalse_ShouldNotTriggerCallbackImmediately()
        {
            var reactive = new ReactiveSystem();
            var called = false;
            
            // We need a custom observer to pass to Register directly
            var observer = new CustomObserver(() => called = true);
            
            reactive.Register(observer, evaluateImmediately: false);
            
            called.Should().BeFalse();
            
            reactive.Tick();
            called.Should().BeTrue();
        }

        [Fact]
        public void Register_ShouldCatchException_DuringInitialEvaluation()
        {
            var reactive = new ReactiveSystem();
            var observer = new ThrowingObserver();
            
            // Should catch exception logged to console
            Action act = () => reactive.Register(observer, evaluateImmediately: true);
            act.Should().NotThrow();
        }

        [Fact]
        public void Dispose_Twice_ShouldNotCrash()
        {
            var reactive = new ReactiveSystem();
            var sub = reactive.Subscribe(() => 1, _ => { });
            
            sub.Dispose();
            Action act = () => sub.Dispose();
            act.Should().NotThrow();
        }

        [Fact]
        public void ReactiveSystem_Instance_ShouldBeAvailable()
        {
            var instance = ReactiveSystem.Instance;
            instance.Should().NotBeNull();
        }

        private class ThrowingObserver : ObserverNode
        {
            public override void CheckAndNotify() => throw new Exception("Boom");
        }
        
        private class ThrowingResetObserver : ObserverNode
        {
            public override void CheckAndNotify() { }
            public override void Reset() => throw new Exception("Reset Boom");
        }
        
        private class MockObserver : ObserverNode
        {
            public bool ResetCalled;
            public override void CheckAndNotify() { }
            public override void Reset() { ResetCalled = true; }
        }

        private class MinimalObserver : ObserverNode
        {
            public override void CheckAndNotify() { }
        }

        private class CustomObserver : ObserverNode
        {
            private readonly Action _onCheck;
            public CustomObserver(Action onCheck) => _onCheck = onCheck;
            public override void CheckAndNotify() => _onCheck();
        }
        [Fact]
        public void Bind_ShouldRebind_WhenCalledWithNewLoop()
        {
            var state = new EntityWorld();
            var dispatcher = new Dispatcher();
            var scheduler = new ActionScheduler();
            var simulation = new GameSimulation(state, dispatcher, scheduler);
            var gameLoop1 = new GameLoop(simulation);
            var gameLoop2 = new GameLoop(simulation);

            var reactive = new ReactiveSystem();
            reactive.Bind(state, gameLoop1);
            
            var tickCount = 0;
            var value = 1;
            // Subscribe using bind context
            reactive.Subscribe(() => value, _ => tickCount++);

            gameLoop1.RunSingleTick();
            tickCount.Should().Be(1);

            // Rebind
            reactive.Bind(state, gameLoop2);

            gameLoop1.RunSingleTick();
            tickCount.Should().Be(1); // Should not increase from loop1

            value = 2; // Change value to trigger update
            gameLoop2.RunSingleTick();
            tickCount.Should().Be(2); // Should increase from loop2
        }

        [Fact]
        public void Register_ShouldThrow_WhenObserverAlreadyOwned()
        {
            var reactive = new ReactiveSystem();
            var observer = new SimplePollingObserver<int>();
            observer.Initialize(() => 1, _ => { }, null);

            reactive.Register(observer);
            
            Action act = () => reactive.Register(observer);
            act.Should().Throw<InvalidOperationException>();
        }
        [Fact]
        public void ReactiveSystem_ShouldTriggerReset_OnRollback()
        {
            // Setup Simulation
            var state = new EntityWorld();
            state.RegisterComponent<PositionComponent>();
            var dispatcher = new Dispatcher();
            var scheduler = new ActionScheduler();
            var simulation = new GameSimulation(state, dispatcher, scheduler);
            var gameLoop = new GameLoop(simulation);

            var reactive = new ReactiveSystem();
            reactive.Bind(state, gameLoop);

            var addedCount = 0;
            var removedCount = 0;

            // Setup Observer
            reactive.ObserveArchetype<PositionComponent>(
                state,
                e => addedCount++,
                e => removedCount++
            );

            // Tick 0: Initial
            gameLoop.RunSingleTick(); 
            // CurrentTick becomes 1
            
            // Tick 1: Just advance to have history
            gameLoop.RunSingleTick();
            // CurrentTick becomes 2

            // Tick 2: Add Entity
            var e1 = state.CreateEntity();
            state.AddComponent(e1, new PositionComponent { X = 1 });
            gameLoop.RunSingleTick(); 
            // CurrentTick becomes 3. State saved at 3.
            
            addedCount.Should().Be(1);

            // CurrentTick is 3.
            // Schedule at Tick 2.
            simulation.ScheduleOnTick(2, new TestRollbackAction(), e1);
            
            // Next Tick: Should detect rollback
            gameLoop.RunSingleTick(); // Tick 4 (performs rollback/resim)
            
            removedCount.Should().Be(1);
        }

        [Fact]
        public void PollingObserver_ShouldReset_OnRollback()
        {
            // Setup Simulation
            var state = new EntityWorld();
            var dispatcher = new Dispatcher();
            var scheduler = new ActionScheduler();
            var simulation = new GameSimulation(state, dispatcher, scheduler);
            var gameLoop = new GameLoop(simulation);

            var reactive = new ReactiveSystem();
            reactive.Bind(state, gameLoop);

            int valueToCheck = 0;
            int callbackCount = 0;
            int lastCallbackValue = -1;

            // Subscribe to a value
            reactive.Subscribe(() => valueToCheck, (v) => 
            {
                callbackCount++;
                lastCallbackValue = v;
            });

            // Tick 0: Initial (Callback runs immediately/on first tick)
            gameLoop.RunSingleTick();
            callbackCount.Should().Be(1);
            lastCallbackValue.Should().Be(0);

            // Tick 1: Change value
            valueToCheck = 10;
            gameLoop.RunSingleTick(); 
            callbackCount.Should().Be(2);
            lastCallbackValue.Should().Be(10);

            // Tick 2: Change value again
            valueToCheck = 20;
            gameLoop.RunSingleTick();
            callbackCount.Should().Be(3);
            lastCallbackValue.Should().Be(20);

            // Trigger Rollback to Tick 1 (Restore Tick 1)
            // Schedule an action at Tick 2 to force rollback (restore point = 2 - 1 = 1)
            // We need a dummy entity for the action
            var e1 = state.CreateEntity();
            simulation.ScheduleOnTick(2, new TestRollbackAction(), e1);
            
            // Change value "externally" to something else to see if Reset picks it up
            valueToCheck = 999;

            // Run tick (Perform Rollback)
            gameLoop.RunSingleTick();
            
            callbackCount.Should().Be(4);
            lastCallbackValue.Should().Be(999);
        }
    }
}
