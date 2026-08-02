namespace Deterministic.GameFramework.Network.Packets;

public enum PacketType : byte
{
    // Client -> Server
    JoinMatch = 1,
    RequestFullState = 2,
    Batch = 3,
    
    EnqueuePlayer = 4,
    CreateLobby = 5,
    JoinLobby = 6,
    StartLobbyMatch = 7,
    
    // Server -> Client
    TickSnapshot = 10,
    FullState = 11,
    MatchJoined = 12,
    ComponentMapping = 13,
    StateHash = 14,
    
    LobbyCreated = 15,
    LobbyJoined = 16,
    MatchAssignment = 17,

    // Server -> Client (Delta sync mode)
    TickDelta = 18
}
