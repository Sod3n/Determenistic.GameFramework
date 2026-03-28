using System;
using System.Runtime.InteropServices;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Types;

namespace Deterministic.GameFramework.Reactive.Benchmarks
{
    [StableId("00000000-0000-0000-0000-000000000100")]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PositionComponent : IComponent
    {
        public float X;
        public float Y;
    }

    [StableId("00000000-0000-0000-0000-000000000101")]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct VelocityComponent : IComponent
    {
        public float X;
        public float Y;
    }

    [StableId("00000000-0000-0000-0000-000000000102")]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TagComponent : IComponent
    {
        public int TagId;
    }
    
    [StableId("00000000-0000-0000-0000-000000000103")]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct DataComponent : IComponent
    {
        public Guid DataId;
        public int Value1;
        public int Value2;
        public int Value3;
    }
}
