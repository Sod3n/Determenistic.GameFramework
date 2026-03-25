using System;
using System.Runtime.InteropServices;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Types;
using Deterministic.GameFramework.DAR;

namespace Deterministic.GameFramework.ECS.Tests
{
    [StableId("00000000-0000-0000-0000-000000000010")]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct PositionComponent : IComponent
    {
        public int X;
        public int Y;
    }

    [StableId("00000000-0000-0000-0000-000000000011")]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct VelocityComponent : IComponent
    {
        public int X;
        public int Y;
    }

    [StableId("00000000-0000-0000-0000-000000000012")]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TagComponent : IComponent
    {
        public int TagId;
    }

    [StableId("00000000-0000-0000-0000-000000000013")]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct RotationComponent : IComponent
    {
        public float Value;
    }

    [StableId("00000000-0000-0000-0000-000000000099")]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct TestRollbackAction : IAction
    {
        public int Value;
    }

    public class DisposableItem : IDisposable
    {
        public int Id { get; set; }
        public bool IsDisposed { get; private set; }
        private readonly Action? _onDispose;

        public DisposableItem() { }
        public DisposableItem(Action onDispose) => _onDispose = onDispose;

        public void Dispose()
        {
            IsDisposed = true;
            _onDispose?.Invoke();
        }
    }
}
