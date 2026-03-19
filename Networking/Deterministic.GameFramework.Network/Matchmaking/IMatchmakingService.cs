using System;
using System.Threading.Tasks;

namespace Deterministic.GameFramework.Network.Server;

public interface IMatchmakingService
{
    Task EnqueuePlayerAsync(MatchmakingPlayer player);
    Task<Lobby> CreateLobbyAsync(MatchmakingPlayer player, string name);
    Task JoinLobbyAsync(Guid lobbyId, MatchmakingPlayer player);
    Task StartLobbyMatchAsync(Guid lobbyId);
    Task EnqueueLobbyAsync(Guid lobbyId);
}
