using BenchmarkDotNet.Attributes;
using Deterministic.GameFramework.ECS;
using System.Collections.Generic;

namespace Deterministic.GameFramework.Reactive.Benchmarks
{
    [MemoryDiagnoser]
    public class ArchetypeObserverBenchmarks
    {
        private EntityWorld _world;
        private EntityWorld _emptyWorld;
        private ReactiveSystem _reactiveSystem;
        private ReactiveSystem _reactiveSystemEmpty;

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
            
            // Setup populated world
            _world = new EntityWorld();
            _reactiveSystem = new ReactiveSystem();
            
            for (int i = 0; i < EntityCount; i++)
            {
                var e = _world.CreateEntity();
                _world.AddComponent(e, new PositionComponent());
                if (i % 2 == 0) _world.AddComponent(e, new VelocityComponent());
                if (i % 3 == 0) _world.AddComponent(e, new TagComponent());
            }

            // Setup empty world for "Add" benchmarks
            _emptyWorld = new EntityWorld();
            _reactiveSystemEmpty = new ReactiveSystem();
            _reactiveSystemEmpty.ObserveArchetype<PositionComponent>(_emptyWorld, _ => { }, _ => { });
        }

        [Benchmark]
        public void ObserveArchetype_Warmup_FullScan()
        {
            // Measure cost of attaching an observer to a world with many entities
            // We create a new observer each time, but we can't easily unregister efficiently in the benchmark loop without affecting state.
            // So we use a throwaway system for this specific op or just measure creation.
            
            // Actually, we can just create a new observer and NOT register it to the system, 
            // but manually call Initialize which does the scan.
            // But ArchetypeObserver.Initialize is internal or requires access? 
            // We use the public API.
            
            // We will create a NEW ReactiveSystem per invocation to avoid accumulation
            var sys = new ReactiveSystem();
            sys.ObserveArchetype<PositionComponent>(_world, _ => { }, _ => { });
        }

        [Benchmark]
        public void Process_Add_Single_Entity()
        {
            // Reuse entity 0 to avoid growing the world indefinitely
            var e = new Entity(0);
            
            _emptyWorld.AddComponent(e, new PositionComponent());
            _reactiveSystemEmpty.Tick();
            
            _emptyWorld.RemoveComponent<PositionComponent>(e);
            _reactiveSystemEmpty.Tick();
            
            // Clear dirty so next run actually triggers dirty logic if needed
            // This adds some overhead but is necessary for correctness in this microbenchmark loop
            _emptyWorld.ClearDirty();
        }
    }
}
