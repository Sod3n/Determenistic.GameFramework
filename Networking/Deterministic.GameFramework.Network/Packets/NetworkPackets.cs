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
    /// <summary>
    /// The tick originally requested by the client before collision resolution.
    /// When OriginalExecuteTick != ExecuteTick, the action was bumped due to a
    /// slot collision (e.g., two players acting on the same entity at the same tick).
    /// Clients use this to reconcile predictions — removing the stale predicted
    /// action at the original tick and keeping the server's bumped version.
    /// </summary>
    public long OriginalExecuteTick;
    /// <summary>
    /// Client-assigned unique identifier for prediction correlation (Delta-sync mode).
    /// When non-zero, any entity the action's service creates on either side is
    /// auto-tagged with <c>PredictedByAction { PredictionId }</c>. On the client,
    /// that lets <c>DeltaSyncStrategyClient</c> match the server's authoritative
    /// entity back to its predicted counterpart and destroy the ghost. Zero means
    /// "no prediction" (server-originated or GGPO mode) — auto-tagging is skipped.
    /// </summary>
    public long PredictionId;
    public int DataLength;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct TickSnapshotHeader
{
    public long ServerTick;
    public int PayloadLength;
    public long MinAllowedTick;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct FullStateHeader
{
    public long Tick;           // The tick of the state snapshot (confirmed tick)
    public long ServerTick;     // Server's current tick (client catches up to this)
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
