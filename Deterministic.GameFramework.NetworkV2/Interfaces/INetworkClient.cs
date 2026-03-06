using System;
using System.Threading.Tasks;

namespace Deterministic.GameFramework.NetworkV2.Interfaces;

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
    
    // Events
    event Action<byte[]> OnTickSnapshotReceived;
    event Action<byte[]> OnFullStateReceived;
    event Action<byte[]> OnComponentMappingReceived;
    event Action<byte[]> OnStateHashReceived;
    
    event Action OnConnected;
    event Action OnDisconnected;
    
    // Stats/Info
    int Ping { get; }
}
