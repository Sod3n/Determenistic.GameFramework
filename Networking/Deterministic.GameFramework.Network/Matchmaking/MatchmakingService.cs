using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Deterministic.GameFramework.Network.Packets;

namespace Deterministic.GameFramework.Network.Server;

public class MatchmakingService : IMatchmakingService
{
    private readonly MatchManager _matchManager;
    private readonly MatchmakingOptions _options;
    private readonly ConcurrentQueue<MatchmakingPlayer> _playerQueue = new();
    private readonly ConcurrentDictionary<Guid, Lobby> _lobbies = new();

    public MatchmakingService(MatchManager matchManager, MatchmakingOptions options)
    {
        _matchManager = matchManager;
        _options = options;
    }

    public Task EnqueuePlayerAsync(MatchmakingPlayer player)
    {
        _playerQueue.Enqueue(player);
        TryMatchmake();
        return Task.CompletedTask;
    }

    public Task<Lobby> CreateLobbyAsync(MatchmakingPlayer player, string name)
    {
        var lobby = new Lobby
        {
            Name = name,
            OwnerId = player.PlayerId
        };
        lobby.Players.Add(player);
        
        _lobbies[lobby.Id] = lobby;
        
        return Task.FromResult(lobby);
    }

    public Task JoinLobbyAsync(Guid lobbyId, MatchmakingPlayer player)
    {
        if (_lobbies.TryGetValue(lobbyId, out var lobby))
        {
            lock (lobby.Players)
            {
                if (lobby.Players.Any(p => p.PlayerId == player.PlayerId))
                    return Task.CompletedTask;
                    
                lobby.Players.Add(player);
            }
            
            // Notify joiner that they successfully joined
            var lobbyIdBytes = lobbyId.ToByteArray();
            _ = player.Peer.SendAsync(lobbyIdBytes, PacketType.LobbyJoined);
            
            // Notify other players in lobby
            foreach (var p in lobby.Players)
            {
                if (p.PlayerId != player.PlayerId)
                {
                    // For now, we don't have a specific packet for "Other Player Joined".
                    // We can implement this later.
                }
            }
        }
        return Task.CompletedTask;
    }

    public Task StartLobbyMatchAsync(Guid lobbyId, byte[]? initialState = null)
    {
        if (_lobbies.TryGetValue(lobbyId, out var lobby))
        {
            // Create Match
            var matchId = Guid.NewGuid();
            var match = _matchManager.CreateMatch(matchId, initialState);
            
            // Assign all players
            foreach (var player in lobby.Players)
            {
                NotifyMatchAssignment(player, matchId);
            }
            
            _lobbies.TryRemove(lobbyId, out _);
        }
        return Task.CompletedTask;
    }

    public Task EnqueueLobbyAsync(Guid lobbyId)
    {
        // TODO: Implement team matchmaking
        // For now, just a placeholder
        return Task.CompletedTask;
    }

    private void TryMatchmake()
    {
        // Simple FIFO matchmaking
        if (_playerQueue.Count >= _options.MaxPlayersPerMatch)
        {
            var matchId = Guid.NewGuid();
            var match = _matchManager.CreateMatch(matchId);
            
            for (int i = 0; i < _options.MaxPlayersPerMatch; i++)
            {
                if (_playerQueue.TryDequeue(out var player))
                {
                    NotifyMatchAssignment(player, matchId);
                }
            }
        }
    }
    
    private void NotifyMatchAssignment(MatchmakingPlayer player, Guid matchId)
    {
        var data = matchId.ToByteArray();
        _ = player.Peer.SendAsync(data, PacketType.MatchAssignment);
    }
}
