using System.Collections.Generic;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using Deterministic.GameFramework.DAR;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Benchmarks
{
    [MemoryDiagnoser]
    public class ActionBenchmark
    {
        private EntityWorld _state = null!;
        private Dispatcher _dispatcher = null!;
        private Entity _target;
        private BenchmarkAction _action;

        [GlobalSetup]
        public void Setup()
        {
            ComponentId.RegisterAssembly(typeof(ActionBenchmark).Assembly);
            ComponentId.RegisterAssembly(typeof(World).Assembly);
            _state = new EntityWorld();
            // Dispatcher no longer needs service lookup for registration
            _dispatcher = new Dispatcher();
            
            _target = _state.CreateEntity();
            _state.AddComponent(_target, new BenchmarkComponent());
            
            _action = new BenchmarkAction { Value = 123 };

            // Manually register action service
            var service = new BenchmarkActionService();
            _dispatcher.RegisterAction(service);
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

    [StableId("00000000-0000-0000-0000-000000000008")]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BenchmarkAction : IAction
    {
        public int Value;
    }

    [StableId("00000000-0000-0000-0000-000000000005")]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
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
