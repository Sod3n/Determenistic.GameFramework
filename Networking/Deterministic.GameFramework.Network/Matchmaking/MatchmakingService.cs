using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Deterministic.GameFramework.Common;
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
            // Serialize with StartLobbyMatchAsync so a late JoinLobby that arrives DURING
            // CreateMatch waits for it to finish — otherwise lobby.MatchId is still null
            // and the joiner is never routed to the running match.
            Guid? capturedMatchId;
            int playerCountAfter;
            lock (lobby)
            {
                if (lobby.Players.Any(p => p.PlayerId == player.PlayerId))
                {
                    Deterministic.GameFramework.Utils.Logging.ILogger.Log($"[MatchmakingService] JoinLobby {lobbyId}: player {player.PlayerId} already in lobby — skipping.");
                    return Task.CompletedTask;
                }

                lobby.Players.Add(player);
                capturedMatchId = lobby.MatchId;
                playerCountAfter = lobby.Players.Count;
            }

            Deterministic.GameFramework.Utils.Logging.ILogger.Log($"[MatchmakingService] JoinLobby {lobbyId}: added player {player.PlayerId} (total {playerCountAfter}, matchStarted={capturedMatchId.HasValue}).");

            // Notify joiner that they successfully joined
            var lobbyIdBytes = lobbyId.ToByteArray();
            _ = player.Peer.SendAsync(lobbyIdBytes, PacketType.LobbyJoined);

            // Late join: if the match was already running when we joined, route this player to it.
            if (capturedMatchId.HasValue)
            {
                Deterministic.GameFramework.Utils.Logging.ILogger.Log($"[MatchmakingService] JoinLobby {lobbyId}: routing player {player.PlayerId} to running match {capturedMatchId.Value}.");
                NotifyMatchAssignment(player, capturedMatchId.Value);
            }
        }
        else
        {
            Deterministic.GameFramework.Utils.Logging.ILogger.LogError($"[MatchmakingService] JoinLobby {lobbyId}: lobby not found (player {player.PlayerId}). Late joiner is hung.");
        }
        return Task.CompletedTask;
    }

    public Task StartLobbyMatchAsync(Guid lobbyId, byte[]? initialState = null)
    {
        Deterministic.GameFramework.Utils.Logging.ILogger.Log($"[MatchmakingService] StartLobbyMatch {lobbyId}: ENTER (initialState={(initialState?.Length ?? 0)} bytes).");
        try
        {
            if (_lobbies.TryGetValue(lobbyId, out var lobby))
            {
                // Hold lobby lock for the whole CreateMatch — a JoinLobby that arrives meanwhile
                // would otherwise read lobby.MatchId == null and silently drop the late joiner.
                lock (lobby)
                {
                    var matchId = Guid.NewGuid();
                    Deterministic.GameFramework.Utils.Logging.ILogger.Log($"[MatchmakingService] StartLobbyMatch {lobbyId}: calling CreateMatch ({matchId})...");
                    var match = _matchManager.CreateMatch(matchId, initialState, _options.SyncMode);
                    Deterministic.GameFramework.Utils.Logging.ILogger.Log($"[MatchmakingService] StartLobbyMatch {lobbyId}: CreateMatch returned. Notifying {lobby.Players.Count} players...");

                    foreach (var player in lobby.Players)
                    {
                        NotifyMatchAssignment(player, matchId);
                    }

                    lobby.MatchId = matchId;
                    Deterministic.GameFramework.Utils.Logging.ILogger.Log($"[MatchmakingService] StartLobbyMatch {lobbyId} -> match {matchId} ({lobby.Players.Count} players assigned). Lobby kept alive for late joiners.");
                }
            }
            else
            {
                Deterministic.GameFramework.Utils.Logging.ILogger.LogError($"[MatchmakingService] StartLobbyMatch {lobbyId}: lobby not found.");
            }
        }
        catch (Exception ex)
        {
            Deterministic.GameFramework.Utils.Logging.ILogger.LogError($"[MatchmakingService] StartLobbyMatch {lobbyId}: EXCEPTION: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            throw;
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
            var match = _matchManager.CreateMatch(matchId, mode: _options.SyncMode);

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
