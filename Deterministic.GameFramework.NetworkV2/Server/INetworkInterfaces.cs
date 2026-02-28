using System;
using System.Threading.Tasks;
using Deterministic.GameFramework.NetworkV2.Packets;

namespace Deterministic.GameFramework.NetworkV2.Server;

public interface INetworkPeer
{
    string Id { get; }
    Task SendAsync(byte[] data, PacketType type);
}

public interface INetworkServer
{
    // Managing Groups/Broadcasting
    Task JoinGroupAsync(INetworkPeer peer, string groupName);
    Task LeaveGroupAsync(INetworkPeer peer, string groupName);
    
    Task BroadcastToGroupAsync(string groupName, byte[] data, PacketType type);
}
