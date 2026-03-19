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
    [Collection("Non-Parallel")] public class ReactiveQueryTests
    {
        public ReactiveQueryTests()
        {
            ComponentId.RegisterAssembly(typeof(PositionComponent).Assembly);
            ComponentId.RegisterAssembly(typeof(World).Assembly);
            ComponentId.RegisterAssembly(typeof(ReactiveQueryTests).Assembly);
        }

        [Fact]
        public void ReactiveQuery_T1_T2_ShouldFilterAndNotify()
        {
            var state = new EntityWorld();
            state.RegisterComponent<PositionComponent>();
            state.RegisterComponent<VelocityComponent>();
            
            var reactive = new ReactiveSystem();
            var added = new List<Entity>();

            // Query with component filter
            reactive.ObservableCollection<PositionComponent, VelocityComponent>(state)
                .Where<PositionComponent>(p => p.X > 10)
                .Subscribe(e => added.Add(e), _ => { });

            // Match
            var e1 = state.CreateEntity();
            state.AddComponent(e1, new PositionComponent { X = 20 });
            state.AddComponent(e1, new VelocityComponent());

            // No Match (X too low)
            var e2 = state.CreateEntity();
            state.AddComponent(e2, new PositionComponent { X = 5 });
            state.AddComponent(e2, new VelocityComponent());

            // No Match (Missing Velocity)
            var e3 = state.CreateEntity();
            state.AddComponent(e3, new PositionComponent { X = 20 });

            reactive.Tick();

            added.Should().ContainSingle().Which.Id.Should().Be(e1.Id);
        }

        [Fact]
        public void ReactiveQuery_T1_T2_T3_ShouldFilterAndNotify()
        {
            var state = new EntityWorld();
            state.RegisterComponent<PositionComponent>();
            state.RegisterComponent<VelocityComponent>();
            state.RegisterComponent<TagComponent>();
            
            var reactive = new ReactiveSystem();
            var added = new List<Entity>();

            reactive.ObservableCollection<PositionComponent, VelocityComponent, TagComponent>(state)
                .Where<TagComponent>(t => t.TagId == 1)
                .Subscribe(e => added.Add(e), _ => { });

            // Match
            var e1 = state.CreateEntity();
            state.AddComponent(e1, new PositionComponent());
            state.AddComponent(e1, new VelocityComponent());
            state.AddComponent(e1, new TagComponent { TagId = 1 });

            // No Match (Wrong Tag)
            var e2 = state.CreateEntity();
            state.AddComponent(e2, new PositionComponent());
            state.AddComponent(e2, new VelocityComponent());
            state.AddComponent(e2, new TagComponent { TagId = 2 });

            reactive.Tick();

            added.Should().ContainSingle().Which.Id.Should().Be(e1.Id);
        }

        [Fact]
        public void ObservableCollection_ShouldThrow_WhenSystemNotBoundAndNoStateProvided()
        {
            var reactive = new ReactiveSystem();
            // Not bound

            Action act1 = () => reactive.ObservableCollection<PositionComponent>();
            act1.Should().Throw<InvalidOperationException>();

            Action act2 = () => reactive.ObservableCollection<PositionComponent, VelocityComponent>();
            act2.Should().Throw<InvalidOperationException>();

            Action act3 = () => reactive.ObservableCollection<PositionComponent, VelocityComponent, TagComponent>();
            act3.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void ObservableCollection_ShouldWork_WhenSystemBound()
        {
            var state = new EntityWorld();
            var dispatcher = new Dispatcher();
            var scheduler = new ActionScheduler();
            var simulation = new GameSimulation(state, dispatcher, scheduler);
            var gameLoop = new GameLoop(simulation);

            var reactive = new ReactiveSystem();
            reactive.Bind(state, gameLoop);

            Action act1 = () => reactive.ObservableCollection<PositionComponent>();
            act1.Should().NotThrow();

            Action act2 = () => reactive.ObservableCollection<PositionComponent, VelocityComponent>();
            act2.Should().NotThrow();

            Action act3 = () => reactive.ObservableCollection<PositionComponent, VelocityComponent, TagComponent>();
            act3.Should().NotThrow();
        }

        [Fact]
        public void ReactiveQuery_Subscribe_WithMultipleFilters()
        {
            var state = new EntityWorld();
            state.RegisterComponent<PositionComponent>();
            
            var reactive = new ReactiveSystem();
            var added = new List<Entity>();

            reactive.ObservableCollection<PositionComponent>(state)
                .Where<PositionComponent>(p => p.X > 10)
                .Where(e => e.Id > 0) // Arbitrary entity filter
                .Subscribe(e => added.Add(e), _ => { });

            var e1 = state.CreateEntity();
            state.AddComponent(e1, new PositionComponent { X = 20 });

            reactive.Tick();

            added.Should().ContainSingle();
        }
        [Fact]
        public void ReactiveQuery_Where_ShouldHandleMissingComponent()
        {
            var state = new EntityWorld();
            var reactive = new ReactiveSystem();
            
            var added = 0;
            
            // Query for Position, but Filter on Tag
            // Entity will match Position (Mask) but fail Tag filter (HasComponent check)
            reactive.ObservableCollection<PositionComponent>(state)
                .Where<TagComponent>(t => t.TagId > 0)
                .Subscribe(_ => added++, _ => {});
                
            var e = state.CreateEntity();
            state.AddComponent(e, new PositionComponent());
            // No TagComponent added
            
            reactive.Tick();
            
            added.Should().Be(0);
            
            // Now add Tag
            state.AddComponent(e, new TagComponent { TagId = 1 });
            reactive.Tick();
            
            added.Should().Be(1);
        }

        [Fact]
        public void ReactiveQuery_T2_Where_ShouldSafelyHandleMissingComponent()
        {
            var state = new EntityWorld();
            var reactive = new ReactiveSystem();
            
            var query = reactive.ObservableCollection<PositionComponent, VelocityComponent>(state)
                .Where<TagComponent>(t => t.TagId > 0);
            
            var addedCount = 0;
            query.Subscribe(e => addedCount++, _ => { });
            
            var e1 = state.CreateEntity();
            state.AddComponent(e1, new PositionComponent());
            state.AddComponent(e1, new VelocityComponent());
            state.AddComponent(e1, new TagComponent { TagId = 10 });
            
            var e2 = state.CreateEntity();
            state.AddComponent(e2, new PositionComponent());
            state.AddComponent(e2, new VelocityComponent());
            
            reactive.Tick();
            
            addedCount.Should().Be(1);
        }

        [Fact]
        public void ReactiveQuery_T3_Where_ShouldSafelyHandleMissingComponent()
        {
            var state = new EntityWorld();
            var reactive = new ReactiveSystem();
            
            var query = reactive.ObservableCollection<PositionComponent, VelocityComponent, TagComponent>(state)
                .Where<RotationComponent>(r => r.Value > 0); // RotationComponent not in T1,T2,T3
            
            var addedCount = 0;
            query.Subscribe(e => addedCount++, _ => { });
            
            // Entity with all 3 required components but NO RotationComponent
            var e1 = state.CreateEntity();
            state.AddComponent(e1, new PositionComponent());
            state.AddComponent(e1, new VelocityComponent());
            state.AddComponent(e1, new TagComponent());
            
            // Should NOT match because Where predicate fails (component missing), should NOT crash
            reactive.Tick();
            
            addedCount.Should().Be(0);
            
            // Add entity WITH RotationComponent
            var e2 = state.CreateEntity();
            state.AddComponent(e2, new PositionComponent());
            state.AddComponent(e2, new VelocityComponent());
            state.AddComponent(e2, new TagComponent());
            state.AddComponent(e2, new RotationComponent { Value = 10 });
            
            reactive.Tick();
            addedCount.Should().Be(1);
        }
    }
}
