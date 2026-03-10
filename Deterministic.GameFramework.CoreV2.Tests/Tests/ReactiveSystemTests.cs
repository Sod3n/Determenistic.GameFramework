using System;
using System.Collections.Generic;
using Deterministic.GameFramework.CoreV2;
using Deterministic.GameFramework.CoreV2.Example.Components;
using Deterministic.GameFramework.CoreV2.Extensions;
using Deterministic.GameFramework.Reactive;
using FluentAssertions;
using Xunit;

namespace Deterministic.GameFramework.CoreV2.Tests
{
    public class ReactiveSystemTests
    {
        public ReactiveSystemTests()
        {
            ServiceLocator.RegisterAssembly(typeof(HealthComponent).Assembly);
            ServiceLocator.RegisterAssembly(typeof(World).Assembly);
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
            var state = new GlobalState();
            var entity = state.CreateEntity();
            state.AddComponent(entity, new HealthComponent { CurrentHealth = 100 });

            var reactive = new ReactiveSystem();
            var callbackCount = 0;
            var lastHealth = 0;

            reactive.Subscribe(
                context: state,
                selector: (s) => s.GetComponent<HealthComponent>(entity).CurrentHealth.Value,
                callback: (s, health) =>
                {
                    callbackCount++;
                    lastHealth = health;
                });

            reactive.Tick();
            callbackCount.Should().Be(1);
            lastHealth.Should().Be(100);

            state.GetComponent<HealthComponent>(entity).CurrentHealth = 50;
            reactive.Tick();
            callbackCount.Should().Be(2);
            lastHealth.Should().Be(50);

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
        public void Observer_ShouldBePooled_AfterDispose()
        {
            var reactive = new ReactiveSystem();
            var value = 10;

            var sub1 = reactive.Subscribe(() => value, (v) => { });
            sub1.Dispose();

            var sub2 = reactive.Subscribe(() => value, (v) => { });
            
            sub1.Should().BeSameAs(sub2);
        }

        [Fact]
        public void Bind_ShouldSubscribeToGameLoopTick()
        {
            var state = new GlobalState();
            var scheduler = new ActionScheduler();
            var dispatcher = new Dispatcher();
            var gameLoop = new GameLoop(state, dispatcher, scheduler);

            var reactive = new ReactiveSystem();
            reactive.Bind(state);

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
            var state = new GlobalState();
            var scheduler = new ActionScheduler();
            var dispatcher = new Dispatcher();
            var gameLoop = new GameLoop(state, dispatcher, scheduler);

            var reactive = new ReactiveSystem();
            reactive.Bind(state);

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
            var state = new GlobalState();
            var scheduler = new ActionScheduler();
            var dispatcher = new Dispatcher();
            var gameLoop = new GameLoop(state, dispatcher, scheduler);

            var reactive = new ReactiveSystem();
            reactive.Bind(state);

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
            var state = new GlobalState();
            var entity = state.CreateEntity();
            var ctx = new Context(state, entity);
            state.AddComponent(entity, new HealthComponent { CurrentHealth = 100 });

            var reactive = new ReactiveSystem();
            var values = new List<int>();
                
            var health = entity.GetComponent<HealthComponent>(ctx);

            reactive.Subscribe(
                state,
                s => s.GetComponent<HealthComponent>(entity).CurrentHealth.Value,
                (s, v) => values.Add(v));
            
            reactive.Subscribe(
                state,
                s => s.GetComponent<HealthComponent>(entity).CurrentHealth.Value,
                (s, v) => values.Add(v * 2));

            reactive.Tick();
            values.Should().Equal(100, 200);

            values.Clear();
            state.GetComponent<HealthComponent>(entity).CurrentHealth = 50;
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
        public void ObserverRegistration_DuringTick_ShouldBeDeferred()
        {
            var reactive = new ReactiveSystem();
            var value = 10;
            var callback1Count = 0;
            var callback2Count = 0;

            reactive.Subscribe(() => value, (v) =>
            {
                callback1Count++;
                if (callback1Count == 1)
                {
                    reactive.Subscribe(() => value, (v2) => callback2Count++);
                }
            });

            reactive.Tick();
            callback1Count.Should().Be(1);
            callback2Count.Should().Be(1);

            value = 20;
            reactive.Tick();
            callback1Count.Should().Be(2);
            callback2Count.Should().Be(2);
        }
    }
}