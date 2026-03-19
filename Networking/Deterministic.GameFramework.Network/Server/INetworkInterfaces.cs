using System;
using System.Threading.Tasks;
using Deterministic.GameFramework.Network.Packets;

namespace Deterministic.GameFramework.Network.Server;

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
