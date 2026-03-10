using BenchmarkDotNet.Attributes;
using Deterministic.GameFramework.CoreV2;

namespace Deterministic.GameFramework.Benchmarks
{
    [MemoryDiagnoser]
    public class ECSBenchmark
    {
        private GlobalState _state = null!;
        private const int EntityCount = 100_000;

        [GlobalSetup]
        public void Setup()
        {
            ComponentId.RegisterAssembly(typeof(ECSBenchmark).Assembly);
            ComponentId.RegisterAssembly(typeof(GlobalState).Assembly);
            _state = new GlobalState();
            
            for (int i = 0; i < EntityCount; i++)
            {
                var entity = _state.CreateEntity();
                _state.AddComponent(entity, new TransformComponent { X = i, Y = i });
                _state.AddComponent(entity, new VelocityComponent { X = 1, Y = 1 });
            }
        }

        [Benchmark]
        public void ForEach_Struct_Ref()
        {
            _state.ForEach((ref TransformComponent pos, ref VelocityComponent vel) =>
            {
                pos.X += vel.X;
                pos.Y += vel.Y;
            });
        }

        [Benchmark]
        public void Manual_Iteration()
        {
            var transforms = _state.GetRawArray<TransformComponent>();
            var velocities = _state.GetRawArray<VelocityComponent>();
            var masks = _state.EntityMasks;
            
            // Reconstruct mask logic for fair comparison
            var tMask = new BitMask128();
            tMask.Set(ComponentId<TransformComponent>.IntId);
            tMask.Set(ComponentId<VelocityComponent>.IntId);

            int count = masks.Length;
            for (int i = 0; i < count; i++)
            {
                if (masks[i].HasAll(tMask))
                {
                    ref var pos = ref transforms[i];
                    ref var vel = ref velocities[i];
                    pos.X += vel.X;
                    pos.Y += vel.Y;
                }
            }
        }
    }

    [NetworkId("00000000-0000-0000-0000-000000000001")]
    public struct TransformComponent : IComponent
    {
        public Float X;
        public Float Y;
    }

    [NetworkId("00000000-0000-0000-0000-000000000002")]
    public struct VelocityComponent : IComponent
    {
        public Float X;
        public Float Y;
    }
}
