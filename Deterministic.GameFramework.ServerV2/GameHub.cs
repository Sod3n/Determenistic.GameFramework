using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Deterministic.GameFramework.NetworkV2.Server;

namespace Deterministic.GameFramework.ServerV2;

public class GameHub : Hub
{
    private readonly GamePacketProcessor _processor;
    private readonly IAuthService _authService;

    public GameHub(GamePacketProcessor processor, IAuthService authService)
    {
        _processor = processor;
        _authService = authService;
    }

    public async Task JoinMatch(Guid matchId, string? authToken = null)
    {
        var peer = new SignalRPeer(Clients.Caller, Context.ConnectionId);
        await _processor.JoinMatchAsync(matchId, authToken, peer, _authService);
        
        // Context items still useful for local tracking if needed, 
        // but processor handles the "JoinGroup" logic via INetworkServer
        Context.Items["matchId"] = matchId.ToString();
    }

    public async Task SendBatch(byte[] payload)
    {
        if (Context.Items.TryGetValue("matchId", out var matchIdObj) && Guid.TryParse(matchIdObj?.ToString(), out var matchId))
        {
            var peer = new SignalRPeer(Clients.Caller, Context.ConnectionId);
            await _processor.ProcessBatchAsync(matchId, payload, peer);
        }
    }

    public async Task RequestFullState(Guid matchId)
    {
        var peer = new SignalRPeer(Clients.Caller, Context.ConnectionId);
        await _processor.RequestFullStateAsync(matchId, peer);
    }
}
