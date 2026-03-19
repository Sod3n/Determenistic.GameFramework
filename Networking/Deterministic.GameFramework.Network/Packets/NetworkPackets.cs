using System.Runtime.InteropServices;
using Deterministic.GameFramework.ECS;
using Guid = System.Guid;

namespace Deterministic.GameFramework.Network.Packets;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct NetworkActionHeader
{
    public DenseComponentId ComponentId;
    public int TargetEntityId;
    public long ExecuteTick;
    public int DataLength;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct TickSnapshotHeader
{
    public long ServerTick;
    public int PayloadLength;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct FullStateHeader
{
    public long Tick;
    public int StateDataLength;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct MatchJoinedPacket
{
    public Guid PlayerId;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct StateHashPacket
{
    public long Tick;
    public Guid Hash;
}
