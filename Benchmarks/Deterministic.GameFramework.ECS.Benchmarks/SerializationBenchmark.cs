using BenchmarkDotNet.Attributes;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Types;
using System.Runtime.InteropServices;
using System.Reflection;

namespace Deterministic.GameFramework.Benchmarks
{
    [MemoryDiagnoser]
    public class SerializationBenchmark
    {
        private EntityWorld _sourceState = null!;
        private EntityWorld _targetState = null!;
        private byte[] _serializedData = null!;
        private const int EntityCount = 10_000;

        private EntityWorld _bigSourceState = null!;
        private EntityWorld _bigTargetState = null!;
        private byte[] _bigSerializedData = null!;
        private const int BigEntityCount = 100_000;

        [GlobalSetup]
        public void Setup()
        {
            // Register types for ComponentId
            ComponentId.RegisterAssembly(typeof(SerializationBenchmark).Assembly);
            ComponentId.RegisterAssembly(typeof(World).Assembly); // For World component

            // Normal State (10k)
            _sourceState = new EntityWorld();
            _targetState = new EntityWorld();
            _sourceState.RegisterComponent<SerTransformComponent>();
            _sourceState.RegisterComponent<SerVelocityComponent>();
            _targetState.RegisterComponent<SerTransformComponent>();
            _targetState.RegisterComponent<SerVelocityComponent>();

            FillState(_sourceState, EntityCount);
            _serializedData = StateSerializer.Serialize(_sourceState);

            // Big State (100k)
            _bigSourceState = new EntityWorld();
            _bigTargetState = new EntityWorld();
            _bigSourceState.RegisterComponent<SerTransformComponent>();
            _bigSourceState.RegisterComponent<SerVelocityComponent>();
            _bigTargetState.RegisterComponent<SerTransformComponent>();
            _bigTargetState.RegisterComponent<SerVelocityComponent>();

            FillState(_bigSourceState, BigEntityCount);
            _bigSerializedData = StateSerializer.Serialize(_bigSourceState);
        }

        private void FillState(EntityWorld state, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var entity = state.CreateEntity();
                state.AddComponent(entity, new SerTransformComponent { X = i, Y = i, Z = i });
                if (i % 2 == 0)
                {
                    state.AddComponent(entity, new SerVelocityComponent { X = 1, Y = 1 });
                }
            }
        }

        [Benchmark]
        public byte[] Serialize_10k()
        {
            return StateSerializer.Serialize(_sourceState);
        }

        [Benchmark]
        public void Deserialize_10k_FullSync()
        {
            StateSerializer.Deserialize(_targetState, _serializedData, syncComponentIds: true);
        }

        [Benchmark]
        public void Deserialize_10k_Rollback()
        {
            StateSerializer.Deserialize(_targetState, _serializedData, syncComponentIds: false);
        }

        [Benchmark]
        public byte[] Serialize_100k()
        {
            return StateSerializer.Serialize(_bigSourceState);
        }

        [Benchmark]
        public void Deserialize_100k_FullSync()
        {
            StateSerializer.Deserialize(_bigTargetState, _bigSerializedData, syncComponentIds: true);
        }

        [Benchmark]
        public void Deserialize_100k_Rollback()
        {
            StateSerializer.Deserialize(_bigTargetState, _bigSerializedData, syncComponentIds: false);
        }
    }

    [StableId("00000000-0000-0000-0000-000000000003")]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SerTransformComponent : IComponent
    {
        public Float X;
        public Float Y;
        public Float Z;
    }

    [StableId("00000000-0000-0000-0000-000000000004")]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SerVelocityComponent : IComponent
    {
        public Float X;
        public Float Y;
    }
}
