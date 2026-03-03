namespace Deterministic.GameFramework.NetworkV2.Packets;

public enum PacketType : byte
{
    // Client -> Server
    JoinMatch = 1,
    RequestFullState = 2,
    Batch = 3,
    
    // Server -> Client
    TickSnapshot = 10,
    FullState = 11,
    MatchJoined = 12
}
