using BenchmarkDotNet.Attributes;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Common;
using Deterministic.GameFramework.DAR;

namespace Deterministic.GameFramework.Reactive.Benchmarks
{
    [MemoryDiagnoser]
    public class ReactiveSystemBenchmarks
    {
        private EntityWorld _world;
        private ReactiveSystem _reactiveSystem;
        private GameLoop _gameLoop;
        
        [Params(100, 1000)]
        public int EntityCount;

        [GlobalSetup]
        public void Setup()
        {
            try
            {
                ComponentId.RegisterAssembly(typeof(PositionComponent).Assembly);
                ComponentId.RegisterAssembly(typeof(World).Assembly);
            }
            catch { /* Ignore if already registered */ }
            
            _world = new EntityWorld();
            var dispatcher = new Dispatcher();
            var scheduler = new ActionScheduler();
            var simulation = new GameSimulation(_world, dispatcher, scheduler);
            _gameLoop = new GameLoop(simulation);
            
            _reactiveSystem = new ReactiveSystem();
            _reactiveSystem.Bind(_world, _gameLoop);

            // Register observers
            _reactiveSystem.ObserveArchetype<PositionComponent>(_world, _ => { }, _ => { });
            _reactiveSystem.ObserveArchetype<PositionComponent, VelocityComponent>(_world, _ => { }, _ => { });
            
            // Create entities
            for (int i = 0; i < EntityCount; i++)
            {
                var e = _world.CreateEntity();
                _world.AddComponent(e, new PositionComponent());
                if (i % 2 == 0)
                {
                    _world.AddComponent(e, new VelocityComponent());
                }
            }
            
            // Initial tick to process adds
            _reactiveSystem.Tick();
            _world.ClearDirty();
        }

        [Benchmark]
        public void Tick_NoChanges()
        {
            _reactiveSystem.Tick();
        }

        [Benchmark]
        public void Tick_WithChanges()
        {
            // Toggle component on an existing entity to avoid growing the world indefinitely
            var e = new Entity(0);
            
            // Entity 0 starts with Position + Velocity
            _world.RemoveComponent<VelocityComponent>(e);
            _reactiveSystem.Tick();
            
            _world.AddComponent(e, new VelocityComponent());
            _reactiveSystem.Tick();
        }
    }
}
