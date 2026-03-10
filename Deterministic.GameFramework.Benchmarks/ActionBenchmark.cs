using BenchmarkDotNet.Attributes;
using Deterministic.GameFramework.CoreV2;
using System;
using System.Collections.Generic;

namespace Deterministic.GameFramework.Benchmarks
{
    [MemoryDiagnoser]
    public class ActionBenchmark
    {
        private GlobalState _state = null!;
        private Dispatcher _dispatcher = null!;
        private Entity _target;
        private BenchmarkAction _action;

        [GlobalSetup]
        public void Setup()
        {
            _state = new GlobalState();
            // Dispatcher no longer needs service lookup for registration
            _dispatcher = new Dispatcher();
            
            _target = _state.CreateEntity();
            _state.AddComponent(_target, new BenchmarkComponent());
            
            _action = new BenchmarkAction { Value = 123 };

            // Manually register action service
            var service = new BenchmarkActionService();
            _dispatcher.RegisterAction(service, new List<ReactionService<BenchmarkAction, BenchmarkComponent>>());
        }

        [Benchmark]
        public void Execute_Action()
        {
            _dispatcher.Execute(_action, _state, _target);
        }

        [Benchmark]
        public int GetDenseId()
        {
            return _dispatcher.GetDenseId<BenchmarkAction>();
        }
    }

    [NetworkId("00000000-0000-0000-0000-000000000008")]
    public struct BenchmarkAction : IAction
    {
        public int Value;
    }

    [NetworkId("00000000-0000-0000-0000-000000000005")]
    public struct BenchmarkComponent : IComponent
    {
        public int Value;
    }

    public class BenchmarkActionService : ActionService<BenchmarkAction, BenchmarkComponent>
    {
        protected override void ExecuteProcess(BenchmarkAction action, ref BenchmarkComponent component, Context context)
        {
            component.Value += action.Value;
        }
    }
}
