using BenchmarkDotNet.Attributes;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Reactive;
using R3;
using System.Linq;

namespace Deterministic.GameFramework.Reactive.Benchmarks
{
    [MemoryDiagnoser]
    public class QueryBenchmarks
    {
        private EntityWorld _world;
        private ReactiveSystem _reactiveSystem;
        private Entity _updateEntity;

        [Params(1000, 10000)]
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
            _reactiveSystem = new ReactiveSystem();
            
            for (int i = 0; i < EntityCount; i++)
            {
                var e = _world.CreateEntity();
                _world.AddComponent(e, new PositionComponent { X = i });
                if (i % 2 == 0) _world.AddComponent(e, new TagComponent { TagId = 1 });
                if (i % 4 == 0) _world.AddComponent(e, new VelocityComponent());
            }

            // Setup a query
            _reactiveSystem.ObservableCollection<PositionComponent>(_world)
                .Where<TagComponent>(t => t.TagId == 1)
                .Subscribe(_ => { }, _ => { });
                
            _updateEntity = _world.CreateEntity();
            _world.AddComponent(_updateEntity, new PositionComponent());
            _world.AddComponent(_updateEntity, new TagComponent { TagId = 1 });
            
            // Clear dirty state from setup so benchmarks only measure the incremental update
            _world.ClearDirty();
        }

        [Benchmark]
        public void Filter_Standard_Iteration()
        {
            // Baseline: Manual iteration over filter
            int count = 0;
            foreach(var e in _world.Filter<PositionComponent>())
            {
                if (_world.HasComponent<TagComponent>(e)) count++;
            }
        }

        [Benchmark]
        public void Reactive_Tick_With_Filter()
        {
            // Measure tick cost when we have a filtered query and one dirty entity matches
            
            // We toggle the entity to be dirty
            // _world.AddComponent calls MarkDirty
            _world.AddComponent(_updateEntity, new PositionComponent { X = 1 });
            
            _reactiveSystem.Tick();
            
            // Clear dirty so we don't accumulate dirty entities (though it's the same entity here)
            // But critically, we want to simulate the steady state where previous dirty entities are cleared
            _world.ClearDirty();
        }
    }
}
