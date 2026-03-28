using System;
using System.Collections.Generic;
using System.Linq;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Reactive;
using Deterministic.GameFramework.Types;
using Deterministic.GameFramework.Common;
using Deterministic.GameFramework.DAR;
using ObservableCollections;
using R3;
using FluentAssertions;
using Xunit;

namespace Deterministic.GameFramework.ECS.Tests
{
    [Collection("Non-Parallel")] public class ReactiveQueryObservableExtensionsTests
    {
        public ReactiveQueryObservableExtensionsTests()
        {
            ComponentId.RegisterAssembly(typeof(PositionComponent).Assembly);
            ComponentId.RegisterAssembly(typeof(World).Assembly);
        }

        [Fact]
        public void ToObservableList_ShouldTrackEntities()
        {
            var state = new EntityWorld();
            state.RegisterComponent<PositionComponent>();
            
            var reactive = new ReactiveSystem();
            var disposables = new CompositeDisposable();

            var list = reactive.ObservableCollection<PositionComponent>(state)
                .ToObservableList((Entity e) => e.Id, disposables);

            list.Count.Should().Be(0);

            // Add
            var e1 = state.CreateEntity();
            state.AddComponent(e1, new PositionComponent { X = 10 });
            reactive.Tick();

            list.Count.Should().Be(1);
            list[0].Should().Be(e1.Id);

            // Remove
            state.RemoveComponent<PositionComponent>(e1);
            reactive.Tick();

            list.Count.Should().Be(0);
        }

        [Fact]
        public void ToObservableList_WithContextSelector_ShouldWork()
        {
            var state = new EntityWorld();
            state.RegisterComponent<PositionComponent>();
            
            var reactive = new ReactiveSystem();
            var disposables = new CompositeDisposable();

            // Use selector that requires Context (e.g. to get component data)
            var list = reactive.ObservableCollection<PositionComponent>(state)
                .ToObservableList((Context ctx) => ctx.GetComponent<PositionComponent>(ctx.Entity).X, disposables);

            var e1 = state.CreateEntity();
            state.AddComponent(e1, new PositionComponent { X = 123 });
            reactive.Tick();

            list.Count.Should().Be(1);
            list[0].Should().Be(123);
        }

        [Fact]
        public void ToObservableList_ShouldDisposeItems_WhenRemoved()
        {
            var state = new EntityWorld();
            state.RegisterComponent<PositionComponent>();
            
            var reactive = new ReactiveSystem();
            var disposables = new CompositeDisposable();

            var disposedCount = 0;
            
            var list = reactive.ObservableCollection<PositionComponent>(state)
                .ToObservableList((Entity e) => new DisposableItem(() => disposedCount++), disposables);

            var e1 = state.CreateEntity();
            state.AddComponent(e1, new PositionComponent());
            reactive.Tick();

            disposedCount.Should().Be(0);

            // Remove entity -> Item removed -> Dispose called
            state.RemoveComponent<PositionComponent>(e1);
            reactive.Tick();

            disposedCount.Should().Be(1);
        }

        [Fact]
        public void ToObservableList_ShouldDisposeAllItems_WhenDisposablesDisposed()
        {
            var state = new EntityWorld();
            state.RegisterComponent<PositionComponent>();
            
            var reactive = new ReactiveSystem();
            var disposables = new CompositeDisposable();

            var disposedCount = 0;
            
            var list = reactive.ObservableCollection<PositionComponent>(state)
                .ToObservableList((Entity e) => new DisposableItem(() => disposedCount++), disposables);

            var e1 = state.CreateEntity();
            state.AddComponent(e1, new PositionComponent());
            
            var e2 = state.CreateEntity();
            state.AddComponent(e2, new PositionComponent());
            
            reactive.Tick();

            list.Count.Should().Be(2);

            // Dispose the whole subscription
            disposables.Dispose();

            list.Count.Should().Be(0); // Should clear list
            disposedCount.Should().Be(2); // Should dispose items
        }

        [Fact]
        public void ReactiveSystem_ObservableList_Extension_ShouldWork()
        {
            var state = new EntityWorld();
            var dispatcher = new Dispatcher();
            var scheduler = new ActionScheduler();
            var simulation = new GameSimulation(state, dispatcher, scheduler);
            var gameLoop = new GameLoop(simulation);
            
            var reactive = new ReactiveSystem();
            reactive.Bind(state, gameLoop);
            var disposables = new CompositeDisposable();

            // Test the extension directly on ReactiveSystem
            var list = reactive.ObservableList<PositionComponent, int>(
                ctx => ctx.GetComponent<PositionComponent>(ctx.Entity).X, 
                disposables);

            var e1 = state.CreateEntity();
            state.AddComponent(e1, new PositionComponent { X = 42 });
            reactive.Tick();

            list.Count.Should().Be(1);
            list[0].Should().Be(42);
        }

        [Fact]
        public void ReactiveSystem_ObservableList_T1_T2_ShouldWork()
        {
            var state = new EntityWorld();
            var reactive = new ReactiveSystem();
            reactive.Bind(state, new GameLoop(new GameSimulation(state, new Dispatcher(), new ActionScheduler())));
            var disposables = new CompositeDisposable();

            var list = reactive.ObservableList<PositionComponent, VelocityComponent, int>(
                ctx => ctx.GetComponent<PositionComponent>(ctx.Entity).X,
                disposables);

            var e = state.CreateEntity();
            state.AddComponent(e, new PositionComponent { X = 10 });
            state.AddComponent(e, new VelocityComponent());
            
            reactive.Tick();
            list.Count.Should().Be(1);
            list[0].Should().Be(10);
        }

        [Fact]
        public void ReactiveSystem_ObservableList_T1_T2_T3_ShouldWork()
        {
            var state = new EntityWorld();
            var reactive = new ReactiveSystem();
            reactive.Bind(state, new GameLoop(new GameSimulation(state, new Dispatcher(), new ActionScheduler())));
            var disposables = new CompositeDisposable();

            var list = reactive.ObservableList<PositionComponent, VelocityComponent, TagComponent, int>(
                ctx => ctx.GetComponent<PositionComponent>(ctx.Entity).X,
                disposables);

            var e = state.CreateEntity();
            state.AddComponent(e, new PositionComponent { X = 20 });
            state.AddComponent(e, new VelocityComponent());
            state.AddComponent(e, new TagComponent());
            
            reactive.Tick();
            list.Count.Should().Be(1);
            list[0].Should().Be(20);
        }

        [Fact]
        public void ReactiveQuery_ToObservableList_T1_T2_WithContextSelector_ShouldWork()
        {
            var state = new EntityWorld();
            var reactive = new ReactiveSystem();
            var query = new ReactiveQuery<PositionComponent, VelocityComponent>(reactive, state);
            var disposables = new CompositeDisposable();

            var list = query.ToObservableList(
                (Context ctx) => ctx.GetComponent<PositionComponent>(ctx.Entity).X + 5,
                disposables);

            var e = state.CreateEntity();
            state.AddComponent(e, new PositionComponent { X = 10 });
            state.AddComponent(e, new VelocityComponent());

            reactive.Tick();
            list[0].Should().Be(15);
        }

        [Fact]
        public void ReactiveQuery_ToObservableList_T1_T2_T3_WithContextSelector_ShouldWork()
        {
            var state = new EntityWorld();
            var reactive = new ReactiveSystem();
            var query = new ReactiveQuery<PositionComponent, VelocityComponent, TagComponent>(reactive, state);
            var disposables = new CompositeDisposable();

            var list = query.ToObservableList(
                (Context ctx) => ctx.GetComponent<PositionComponent>(ctx.Entity).X + 10,
                disposables);

            var e = state.CreateEntity();
            state.AddComponent(e, new PositionComponent { X = 10 });
            state.AddComponent(e, new VelocityComponent());
            state.AddComponent(e, new TagComponent());

            reactive.Tick();
            list[0].Should().Be(20);
        }

        [Fact]
        public void ReactiveQueryObservableExtensions_T2_ShouldDisposeItems_WhenCompositeDisposableDisposed()
        {
            var state = new EntityWorld();
            var reactive = new ReactiveSystem();
            var disposables = new CompositeDisposable();
            
            var list = reactive.ObservableCollection<PositionComponent, VelocityComponent>(state)
                .ToObservableList(e => new DisposableItem { Id = e.Id }, disposables);

            var e = state.CreateEntity();
            state.AddComponent(e, new PositionComponent());
            state.AddComponent(e, new VelocityComponent());
            
            reactive.Tick();
            var item = list[0];
            
            // Dispose the whole subscription
            disposables.Dispose();
            
            list.Count.Should().Be(0);
            item.IsDisposed.Should().BeTrue();
        }

        [Fact]
        public void ReactiveQueryObservableExtensions_T3_ShouldDisposeItems_WhenCompositeDisposableDisposed()
        {
            var state = new EntityWorld();
            var reactive = new ReactiveSystem();
            var disposables = new CompositeDisposable();
            
            var list = reactive.ObservableCollection<PositionComponent, VelocityComponent, TagComponent>(state)
                .ToObservableList(e => new DisposableItem { Id = e.Id }, disposables);

            var e = state.CreateEntity();
            state.AddComponent(e, new PositionComponent());
            state.AddComponent(e, new VelocityComponent());
            state.AddComponent(e, new TagComponent());
            
            reactive.Tick();
            var item = list[0];
            
            // Dispose the whole subscription
            disposables.Dispose();
            
            list.Count.Should().Be(0);
            item.IsDisposed.Should().BeTrue();
        }

        [Fact]
        public void ReactiveQueryObservableExtensions_T2_ShouldHandleRemovalAndDisposal()
        {
            var state = new EntityWorld();
            var reactive = new ReactiveSystem();
            var disposables = new CompositeDisposable();
            
            var list = reactive.ObservableCollection<PositionComponent, VelocityComponent>(state)
                .ToObservableList(e => new DisposableItem { Id = e.Id }, disposables);

            var e = state.CreateEntity();
            state.AddComponent(e, new PositionComponent());
            state.AddComponent(e, new VelocityComponent());
            
            reactive.Tick();
            list.Count.Should().Be(1);
            var item = list[0];
            item.IsDisposed.Should().BeFalse();
            
            // Remove component to trigger removal
            state.RemoveComponent<VelocityComponent>(e);
            reactive.Tick();
            
            list.Count.Should().Be(0);
            item.IsDisposed.Should().BeTrue();
            
            // Cleanup
            disposables.Dispose();
        }

        [Fact]
        public void ReactiveQueryObservableExtensions_T3_ShouldHandleRemovalAndDisposal()
        {
            var state = new EntityWorld();
            var reactive = new ReactiveSystem();
            var disposables = new CompositeDisposable();
            
            var list = reactive.ObservableCollection<PositionComponent, VelocityComponent, TagComponent>(state)
                .ToObservableList(e => new DisposableItem { Id = e.Id }, disposables);

            var e = state.CreateEntity();
            state.AddComponent(e, new PositionComponent());
            state.AddComponent(e, new VelocityComponent());
            state.AddComponent(e, new TagComponent());
            
            reactive.Tick();
            list.Count.Should().Be(1);
            var item = list[0];
            
            // Remove component
            state.RemoveComponent<TagComponent>(e);
            reactive.Tick();
            
            list.Count.Should().Be(0);
            item.IsDisposed.Should().BeTrue();
            
            disposables.Dispose();
        }

        [Fact]
        public void ReactiveQueryObservableExtensions_ShouldDisposeItems_WhenCompositeDisposableDisposed()
        {
            var state = new EntityWorld();
            var reactive = new ReactiveSystem();
            var disposables = new CompositeDisposable();
            
            var list = reactive.ObservableCollection<PositionComponent>(state)
                .ToObservableList(e => new DisposableItem { Id = e.Id }, disposables);

            var e = state.CreateEntity();
            state.AddComponent(e, new PositionComponent());
            
            reactive.Tick();
            var item = list[0];
            
            // Dispose the whole subscription
            disposables.Dispose();
            
            list.Count.Should().Be(0);
            item.IsDisposed.Should().BeTrue();
        }

        private class DisposableItem : IDisposable
        {
            public int Id { get; set; }
            public bool IsDisposed { get; private set; }
            private readonly Action? _onDispose;

            public DisposableItem() { }
            public DisposableItem(Action onDispose) => _onDispose = onDispose;

            public void Dispose()
            {
                IsDisposed = true;
                _onDispose?.Invoke();
            }
        }
    }
}
