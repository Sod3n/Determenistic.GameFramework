using System.Runtime.InteropServices;

namespace Deterministic.GameFramework.NetworkV2.Packets;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct NetworkActionHeader
{
    public int NetworkId;
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
