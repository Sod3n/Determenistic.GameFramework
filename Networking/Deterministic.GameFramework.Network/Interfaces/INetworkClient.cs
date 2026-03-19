using System;
using System.Threading.Tasks;

namespace Deterministic.GameFramework.Network.Interfaces;

public enum DeliveryMethod
{
    ReliableOrdered,
    ReliableUnordered,
    Sequenced,
    Unreliable
}

public interface INetworkClient : IAsyncDisposable
{
    Task ConnectAsync(string address);
    
    // Game Specific Methods - Service Interface pattern
    Task<System.Guid> JoinMatchAsync(System.Guid matchId, string? token = null);
    Task RequestFullStateAsync(System.Guid matchId);
    void SendBatch(byte[] data);
    
    // Matchmaking & Lobby
    Task EnqueuePlayerAsync();
    Task CreateLobbyAsync(string lobbyName);
    Task JoinLobbyAsync(System.Guid lobbyId);
    Task StartLobbyMatchAsync(System.Guid lobbyId);
    
    // Events
    event Action<byte[]> OnTickSnapshotReceived;
    event Action<byte[]> OnFullStateReceived;
    event Action<byte[]> OnComponentMappingReceived;
    event Action<byte[]> OnStateHashReceived;
    
    event Action<System.Guid> OnLobbyCreated;
    event Action<System.Guid> OnLobbyJoined;
    event Action<System.Guid> OnMatchAssigned; // Triggered when queue pops or lobby starts
    
    event Action OnConnected;
    event Action OnDisconnected;
    
    // Stats/Info
    int Ping { get; }
}
