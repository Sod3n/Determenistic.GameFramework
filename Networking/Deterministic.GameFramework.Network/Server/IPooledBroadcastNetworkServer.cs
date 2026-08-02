using System;
using System.Threading.Tasks;
using Deterministic.GameFramework.Network.Packets;

namespace Deterministic.GameFramework.Network.Server;

/// <summary>
/// Optional extension over <see cref="INetworkServer"/> for transports that can consume
/// a pooled/rented <c>byte[]</c> and return it to the pool once the underlying send is
/// complete. Enables steady-state zero-allocation broadcast for the server-authoritative
/// hot path (<see cref="Delta.DeltaSyncStrategyServer"/>).
///
/// Contract: the caller rents a buffer (e.g. from <see cref="System.Buffers.ArrayPool{T}"/>),
/// fills <c>[0, length)</c>, and transfers ownership to the server via
/// <see cref="BroadcastPooledAsync"/>. The server MUST NOT touch the buffer after the
/// returned <see cref="Task"/> completes. If the underlying send is synchronous the buffer
/// is returned to the pool before the method returns.
///
/// Implementations are free to fall back to the standard <see cref="INetworkServer.BroadcastToGroupAsync"/>
/// path if pooling can't be honored (e.g. the transport captures the buffer across an
/// <c>await</c>). In that case the implementation must still take responsibility for
/// returning the buffer.
/// </summary>
public interface IPooledBroadcastNetworkServer : INetworkServer
{
    /// <summary>
    /// Broadcast the first <paramref name="length"/> bytes of <paramref name="rentedBuffer"/>
    /// to <paramref name="groupName"/> as <paramref name="type"/>, then return the buffer
    /// to its pool.
    /// </summary>
    Task BroadcastToGroupPooledAsync(string groupName, byte[] rentedBuffer, int length, PacketType type);
}
