using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Deterministic.GameFramework.Network.Packets;
using Deterministic.GameFramework.Network.Server;

namespace Deterministic.GameFramework.ServerV2;

public class SignalRPeer : INetworkPeer
{
    private readonly IClientProxy _clientProxy;
    private readonly string _connectionId;

    public string Id => _connectionId;

    public SignalRPeer(IClientProxy clientProxy, string connectionId)
    {
        _clientProxy = clientProxy;
        _connectionId = connectionId;
    }

    public async Task SendAsync(byte[] data, PacketType type)
    {
        string method = GetMethodName(type);
        await _clientProxy.SendAsync(method, data);
    }

    private static string GetMethodName(PacketType type)
    {
        return type switch
        {
            PacketType.TickSnapshot => "OnTickSnapshot",
            PacketType.FullState => "OnFullStateReceived",
            PacketType.MatchJoined => "OnMatchJoined",
            PacketType.ComponentMapping => "OnComponentMappingReceived",
            PacketType.StateHash => "OnStateHashReceived",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }
}

public class SignalRNetworkServer : INetworkServer
{
    private readonly IHubContext<GameHub> _hubContext;

    public SignalRNetworkServer(IHubContext<GameHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task JoinGroupAsync(INetworkPeer peer, string groupName)
    {
        // We only support SignalR peers here
        if (peer is SignalRPeer)
        {
            await _hubContext.Groups.AddToGroupAsync(peer.Id, groupName);
        }
    }

    public async Task LeaveGroupAsync(INetworkPeer peer, string groupName)
    {
        if (peer is SignalRPeer)
        {
            await _hubContext.Groups.RemoveFromGroupAsync(peer.Id, groupName);
        }
    }

    public async Task BroadcastToGroupAsync(string groupName, byte[] data, PacketType type)
    {
        string method = GetMethodName(type);
        await _hubContext.Clients.Group(groupName).SendAsync(method, data);
    }

    private static string GetMethodName(PacketType type)
    {
        return type switch
        {
            PacketType.TickSnapshot => "OnTickSnapshot",
            PacketType.FullState => "OnFullStateReceived",
            PacketType.MatchJoined => "OnMatchJoined",
            PacketType.ComponentMapping => "OnComponentMappingReceived",
            PacketType.StateHash => "OnStateHashReceived",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }
}
