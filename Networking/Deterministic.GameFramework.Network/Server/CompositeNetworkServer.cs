using System.Threading.Tasks;
using Deterministic.GameFramework.Network.Packets;

namespace Deterministic.GameFramework.Network.Server;

public class CompositeNetworkServer : INetworkServer
{
    private readonly INetworkServer[] _servers;

    public CompositeNetworkServer(params INetworkServer[] servers)
    {
        _servers = servers;
    }

    public async Task JoinGroupAsync(INetworkPeer peer, string groupName)
    {
        // Peer is specific to a transport, so we just delegate to all?
        // No, the peer implementation knows which server it belongs to implicitly?
        // Actually, JoinGroupAsync in INetworkServer implementations usually checks `if (peer is MyPeerType)`.
        
        foreach (var server in _servers)
        {
            await server.JoinGroupAsync(peer, groupName);
        }
    }

    public async Task LeaveGroupAsync(INetworkPeer peer, string groupName)
    {
        foreach (var server in _servers)
        {
            await server.LeaveGroupAsync(peer, groupName);
        }
    }

    public async Task BroadcastToGroupAsync(string groupName, byte[] data, PacketType type)
    {
        // Broadcast to all servers
        foreach (var server in _servers)
        {
            await server.BroadcastToGroupAsync(groupName, data, type);
        }
    }
}
