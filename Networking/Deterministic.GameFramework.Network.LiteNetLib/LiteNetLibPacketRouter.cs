using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Deterministic.GameFramework.Network.Packets;
using Deterministic.GameFramework.Utils.Logging;
using LiteNetLib.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Deterministic.GameFramework.Network.Server;

public class LiteNetLibPacketRouter
{
    private readonly GamePacketProcessor _processor;
    private readonly IAuthService _authService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<string, Guid> _peerSessions = new();

    public LiteNetLibPacketRouter(GamePacketProcessor processor, IAuthService authService, IServiceProvider serviceProvider)
    {
        _processor = processor;
        _authService = authService;
        _serviceProvider = serviceProvider;
    }

    private IMatchmakingService MatchmakingService => _serviceProvider.GetRequiredService<IMatchmakingService>();

    public Task OnPeerConnected(INetworkPeer peer)
    {
        return Task.CompletedTask;
    }

    public async Task OnPeerDisconnected(INetworkPeer peer)
    {
        if (_peerSessions.TryRemove(peer.Id, out var matchId))
        {
            var matchManager = _serviceProvider.GetRequiredService<MatchManager>();
            var networkServer = _serviceProvider.GetRequiredService<INetworkServer>();
            var match = matchManager.GetMatch(matchId);

            if (match != null)
            {
                var playerId = await _authService.AuthenticateAsync(peer.Id, null);
                match.RemovePlayer(playerId);
                await networkServer.LeaveGroupAsync(peer, matchId.ToString());

                ILogger.Log($"[PacketRouter] Player {playerId} disconnected from match {matchId}. Players remaining: {match.Players.Count}");

                if (match.Players.Count == 0)
                {
                    ILogger.Log($"[PacketRouter] All players left match {matchId}, removing match.");
                    matchManager.RemoveMatch(matchId);
                }
            }
        }
    }

    public async Task OnPacketReceived(INetworkPeer peer, byte[] data)
    {
        if (data.Length == 0) return;

        // Use NetDataReader to parse safely
        var reader = new NetDataReader(data);
        if (!reader.TryGetByte(out byte typeByte)) return;

        PacketType type = (PacketType)typeByte;

        // Log every non-Batch packet so we can see what's reaching the server during lobby setup.
        // Batch packets are per-tick and would flood the log.
        if (type != PacketType.Batch)
            ILogger.Log($"[PacketRouter] Received {type} from peer {peer.Id} ({data.Length} bytes)");

        switch (type)
        {
            case PacketType.JoinMatch:
            {
                // Format: [Type][MatchId(16)][Token(String)]
                if (reader.AvailableBytes < 16) return;
                
                byte[] matchIdBytes = new byte[16];
                reader.GetBytes(matchIdBytes, 16);
                
                var matchId = new Guid(matchIdBytes);
                var token = reader.GetString(); // Handles null/empty safely usually
                
                // Track session
                _peerSessions[peer.Id] = matchId;
                
                await _processor.JoinMatchAsync(matchId, token, peer, _authService);
                break;
            }
            
            case PacketType.Batch:
            {
                if (_peerSessions.TryGetValue(peer.Id, out var matchId))
                {
                    // Payload is everything after the Type byte
                    // We can slice the original array or copy
                    var payload = new byte[data.Length - 1];
                    Array.Copy(data, 1, payload, 0, payload.Length);
                    
                    await _processor.ProcessBatchAsync(matchId, payload, peer);
                }
                break;
            }
            
            case PacketType.RequestFullState:
            {
                if (_peerSessions.TryGetValue(peer.Id, out var existingMatchId))
                {
                    await _processor.RequestFullStateAsync(existingMatchId, peer);
                }
                else
                {
                    if (reader.AvailableBytes >= 16)
                    {
                        byte[] matchIdBytes = new byte[16];
                        reader.GetBytes(matchIdBytes, 16);
                        
                        var requestedMatchId = new Guid(matchIdBytes);
                        _peerSessions[peer.Id] = requestedMatchId; // Implicit join/restore?
                        await _processor.RequestFullStateAsync(requestedMatchId, peer);
                    }
                }
                break;
            }
            
            case PacketType.EnqueuePlayer:
            {
                // No payload, or token? Assume simple enqueue for now
                var playerId = await _authService.AuthenticateAsync(peer.Id, null);
                await MatchmakingService.EnqueuePlayerAsync(new MatchmakingPlayer { PlayerId = playerId, Peer = peer });
                break;
            }
            
            case PacketType.CreateLobby:
            {
                var lobbyName = reader.GetString();
                var playerId = await _authService.AuthenticateAsync(peer.Id, null);
                var lobby = await MatchmakingService.CreateLobbyAsync(new MatchmakingPlayer { PlayerId = playerId, Peer = peer }, lobbyName);
                
                // Respond with LobbyCreated
                await peer.SendAsync(lobby.Id.ToByteArray(), PacketType.LobbyCreated);
                break;
            }
            
            case PacketType.JoinLobby:
            {
                if (reader.AvailableBytes < 16) return;
                byte[] lobbyIdBytes = new byte[16];
                reader.GetBytes(lobbyIdBytes, 16);
                var lobbyId = new Guid(lobbyIdBytes);
                
                var playerId = await _authService.AuthenticateAsync(peer.Id, null);
                await MatchmakingService.JoinLobbyAsync(lobbyId, new MatchmakingPlayer { PlayerId = playerId, Peer = peer });
                break;
            }
            
            case PacketType.StartLobbyMatch:
            {
                if (reader.AvailableBytes < 16) return;
                byte[] lobbyIdBytes = new byte[16];
                reader.GetBytes(lobbyIdBytes, 16);
                var lobbyId = new Guid(lobbyIdBytes);

                byte[]? initialState = null;
                if (reader.AvailableBytes >= 1)
                {
                    byte hasState = reader.GetByte();
                    if (hasState == 1 && reader.AvailableBytes >= 4)
                    {
                        int stateLength = reader.GetInt();
                        if (reader.AvailableBytes >= stateLength)
                        {
                            initialState = new byte[stateLength];
                            reader.GetBytes(initialState, stateLength);
                        }
                    }
                }

                await MatchmakingService.StartLobbyMatchAsync(lobbyId, initialState);
                break;
            }
        }
    }
}
