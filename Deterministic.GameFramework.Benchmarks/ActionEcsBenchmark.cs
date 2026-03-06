using BenchmarkDotNet.Attributes;
using Deterministic.GameFramework.CoreV2;
using System;
using System.Collections.Generic;

namespace Deterministic.GameFramework.Benchmarks
{
    [MemoryDiagnoser]
    public class ActionEcsBenchmark
    {
        private GlobalState _state = null!;
        private Dispatcher _dispatcher = null!;
        private Entity[] _entities = null!;
        private BenchmarkAction _action;
        private BenchmarkActionComponent _actionComponent;
        
        [Params(100, 1000)]
        public int Count { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _state = new GlobalState();
            _entities = new Entity[Count];
            
            // Setup Dispatcher
            _dispatcher = new Dispatcher(type => 
            {
                if (type == typeof(BenchmarkActionService)) return Deterministic.GameFramework.CoreV2.Guid.Parse("00000000-0000-0000-0000-000000000006");
                return Deterministic.GameFramework.CoreV2.Guid.NewGuid();
            });
            var service = new BenchmarkActionService();
            _dispatcher.RegisterAction(service, new List<ReactionService<BenchmarkAction, BenchmarkComponent>>());

            // Fill world
            for (int i = 0; i < Count; i++)
            {
                var e = _state.CreateEntity();
                _state.AddComponent(e, new BenchmarkComponent { Value = 0 });
                _entities[i] = e;
            }
            
            _action = new BenchmarkAction { Value = 1 };
            _actionComponent = new BenchmarkActionComponent { Value = 1 };
        }

        [Benchmark(Baseline = true)]
        public void Dispatcher_Many_Entities()
        {
            // Simulate N entities triggering an action in the same frame
            for (int i = 0; i < Count; i++)
            {
                _dispatcher.Execute(_action, _state, _entities[i]);
            }
        }

        [Benchmark]
        public void ECS_System_Many_Entities()
        {
            // 1. Dispatch Phase: Tag all entities with Action Component
            for (int i = 0; i < Count; i++)
            {
                _state.AddComponent(_entities[i], _actionComponent);
            }

            // 2. System Execution Phase: Process all entities with Action Component
            _state.ForEach((ref BenchmarkActionComponent action, ref BenchmarkComponent target) => 
            {
                target.Value += action.Value;
            });

            // 3. Cleanup Phase: Remove Action Component from all entities
            // Note: In a real ECS, this might be a "ClearAll<T>" which is O(1) or O(N_Active), 
            // but currently we must remove per entity or clear the whole array/mask.
            for (int i = 0; i < Count; i++)
            {
                _state.RemoveComponent<BenchmarkActionComponent>(_entities[i]);
            }
        }
    }

    [NetworkId("00000000-0000-0000-0000-000000000007")]
    public struct BenchmarkActionComponent : IComponent
    {
        public int Value;
    }
}
