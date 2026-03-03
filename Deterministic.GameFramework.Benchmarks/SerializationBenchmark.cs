using BenchmarkDotNet.Attributes;
using Deterministic.GameFramework.CoreV2;
using System.Runtime.InteropServices;

namespace Deterministic.GameFramework.Benchmarks
{
    [MemoryDiagnoser]
    public class SerializationBenchmark
    {
        private GlobalState _sourceState;
        private GlobalState _targetState;
        private byte[] _serializedData;
        private const int EntityCount = 10_000;

        [GlobalSetup]
        public void Setup()
        {
            _sourceState = new GlobalState();
            _targetState = new GlobalState();

            // Ensure types are registered
            var t1 = InternalTypeId<SerTransformComponent>.Value;
            var t2 = InternalTypeId<SerVelocityComponent>.Value;

            for (int i = 0; i < EntityCount; i++)
            {
                var entity = _sourceState.CreateEntity();
                _sourceState.AddComponent(entity, new SerTransformComponent { X = i, Y = i, Z = i });
                if (i % 2 == 0)
                {
                    _sourceState.AddComponent(entity, new SerVelocityComponent { X = 1, Y = 1 });
                }
            }

            // Pre-serialize for deserialization benchmark
            _serializedData = StateSerializer.Serialize(_sourceState);
        }

        [Benchmark]
        public byte[] Serialize()
        {
            return StateSerializer.Serialize(_sourceState);
        }

        [Benchmark]
        public void Deserialize()
        {
            StateSerializer.Deserialize(_targetState, _serializedData);
        }
    }

    [NetworkId("00000000-0000-0000-0000-000000000003")]
    public struct SerTransformComponent : IComponent
    {
        public Float X;
        public Float Y;
        public Float Z;
    }

    [NetworkId("00000000-0000-0000-0000-000000000004")]
    public struct SerVelocityComponent : IComponent
    {
        public Float X;
        public Float Y;
    }
}
