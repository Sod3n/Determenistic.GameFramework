using BenchmarkDotNet.Attributes;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Common;
using Deterministic.GameFramework.DAR;

namespace Deterministic.GameFramework.Reactive.Benchmarks
{
    [MemoryDiagnoser]
    public class ComponentObserverBenchmarks
    {
        private EntityWorld _world;
        private ReactiveSystem _reactiveSystem;
        private Entity[] _entities;
        private IDisposable[] _subscriptions;

        [Params(100, 1000, 10000)]
        public int EntityCount;

        [GlobalSetup]
        public void Setup()
        {
            try
            {
                ComponentId.RegisterAssembly(typeof(PositionComponent).Assembly);
                ComponentId.RegisterAssembly(typeof(World).Assembly);
            }
            catch { }

            _world = new EntityWorld();
            var dispatcher = new Dispatcher();
            var scheduler = new ActionScheduler();
            var simulation = new GameSimulation(_world, dispatcher, scheduler);
            var gameLoop = new GameLoop(simulation);

            _reactiveSystem = new ReactiveSystem();
            _reactiveSystem.Bind(_world, gameLoop);

            _entities = new Entity[EntityCount];
            _subscriptions = new IDisposable[EntityCount];

            for (int i = 0; i < EntityCount; i++)
            {
                _entities[i] = _world.CreateEntity();
                _world.AddComponent(_entities[i], new PositionComponent { X = i, Y = i });
            }

            for (int i = 0; i < EntityCount; i++)
            {
                _subscriptions[i] = _reactiveSystem.SubscribeComponent<PositionComponent>(
                    _world, _entities[i], _ => { });
            }

            _reactiveSystem.Tick();
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            for (int i = 0; i < _subscriptions.Length; i++)
                _subscriptions[i]?.Dispose();
            _reactiveSystem.Dispose();
        }

        [Benchmark]
        public void Tick_NoChanges()
        {
            _reactiveSystem.Tick();
        }

        [Benchmark]
        public void Tick_AllChanged()
        {
            for (int i = 0; i < EntityCount; i++)
                _world.GetComponent<PositionComponent>(_entities[i]).X++;

            _reactiveSystem.Tick();
        }

        [Benchmark]
        public void Tick_10PercentChanged()
        {
            int changed = EntityCount / 10;
            for (int i = 0; i < changed; i++)
                _world.GetComponent<PositionComponent>(_entities[i]).X++;

            _reactiveSystem.Tick();
        }

        [Benchmark]
        public void Subscribe_And_Dispose()
        {
            var sub = _reactiveSystem.SubscribeComponent<PositionComponent>(
                _world, _entities[0], _ => { });
            sub.Dispose();
        }
    }
}
