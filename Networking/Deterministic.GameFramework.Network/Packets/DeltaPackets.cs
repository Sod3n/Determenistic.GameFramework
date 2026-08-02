using System.Runtime.InteropServices;

namespace Deterministic.GameFramework.Network.Packets;

/// <summary>
/// Delta op codes encoded as a single leading byte in the <see cref="PacketType.TickDelta"/>
/// payload stream. Ops are concatenated with no alignment padding and cursor-parsed
/// by the client's DeltaApplier.
///
/// Contract:
/// - <see cref="EntityCreate"/>: entity did not exist in baseline but exists in current. Payload
///   includes entity id, initial mask, and embedded component data for each set bit.
/// - <see cref="EntityDestroy"/>: entity existed in baseline but does not in current. Payload
///   is just the entity id.
/// - <see cref="ComponentAdd"/>: component bit was not set in baseline but is in current.
///   Payload carries the full component bytes.
/// - <see cref="ComponentRemove"/>: component bit was set in baseline but not in current.
///   Payload is just entityId + denseComponentId.
/// - <see cref="ComponentModify"/>: component bit is set in both baseline and current, but
///   bytes differ. Payload carries the full new component bytes (replacement-style, not
///   byte-arithmetic delta — arithmetic reconciliation is computed client-side).
/// </summary>
public enum DeltaOpKind : byte
{
    EntityCreate = 1,
    EntityDestroy = 2,
    ComponentAdd = 3,
    ComponentRemove = 4,
    ComponentModify = 5
}

/// <summary>
/// Header of a <see cref="PacketType.TickDelta"/> packet.
/// <para>
/// A delta represents the state transition from <see cref="BaselineTick"/> to
/// <see cref="ServerTick"/>. Clients must have applied the baseline tick's delta
/// (or a FullState at or after the baseline tick) before consuming this delta.
/// </para>
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct TickDeltaHeader
{
    /// <summary>Tick this delta represents (state post-sim).</summary>
    public long ServerTick;

    /// <summary>Tick of the baseline state. Client must have this tick applied first.</summary>
    public long BaselineTick;

    /// <summary>Bytes of op stream that follow this header.</summary>
    public int PayloadLength;
}
