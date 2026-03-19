using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Deterministic.GameFramework.Network.Server;

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

    public async Task EnqueuePlayer(string? authToken = null)
    {
        var peer = new SignalRPeer(Clients.Caller, Context.ConnectionId);
        var playerId = await _authService.AuthenticateAsync(peer.Id, authToken);
        var mmService = Context.GetHttpContext()?.RequestServices.GetRequiredService<IMatchmakingService>();
        if (mmService != null)
        {
            await mmService.EnqueuePlayerAsync(new MatchmakingPlayer { PlayerId = playerId, Peer = peer });
        }
    }

    public async Task CreateLobby(string name, string? authToken = null)
    {
        var peer = new SignalRPeer(Clients.Caller, Context.ConnectionId);
        var playerId = await _authService.AuthenticateAsync(peer.Id, authToken);
        var mmService = Context.GetHttpContext()?.RequestServices.GetRequiredService<IMatchmakingService>();
        if (mmService != null)
        {
            var lobby = await mmService.CreateLobbyAsync(new MatchmakingPlayer { PlayerId = playerId, Peer = peer }, name);
            // Optionally notify caller of lobby ID
            await Clients.Caller.SendAsync("LobbyCreated", lobby.Id);
        }
    }

    public async Task JoinLobby(Guid lobbyId, string? authToken = null)
    {
        var peer = new SignalRPeer(Clients.Caller, Context.ConnectionId);
        var playerId = await _authService.AuthenticateAsync(peer.Id, authToken);
        var mmService = Context.GetHttpContext()?.RequestServices.GetRequiredService<IMatchmakingService>();
        if (mmService != null)
        {
            await mmService.JoinLobbyAsync(lobbyId, new MatchmakingPlayer { PlayerId = playerId, Peer = peer });
        }
    }

    public async Task StartLobbyMatch(Guid lobbyId)
    {
        var mmService = Context.GetHttpContext()?.RequestServices.GetRequiredService<IMatchmakingService>();
        if (mmService != null)
        {
            await mmService.StartLobbyMatchAsync(lobbyId);
        }
    }

    public async Task EnqueueLobby(Guid lobbyId)
    {
        var mmService = Context.GetHttpContext()?.RequestServices.GetRequiredService<IMatchmakingService>();
        if (mmService != null)
        {
            await mmService.EnqueueLobbyAsync(lobbyId);
        }
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
