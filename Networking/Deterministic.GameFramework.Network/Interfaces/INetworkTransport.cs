using System;
using System.Threading.Tasks;

namespace Deterministic.GameFramework.Network.Interfaces;

/// <summary>
/// Abstraction for network transport layer.
/// </summary>
public interface INetworkTransport
{
    Task ConnectAsync();
    Task DisconnectAsync();
    
    // Send a message to the server/client
    void Send(string method, object? arg1);
    void Send(string method, object? arg1, object? arg2);
    
    // Register a handler for messages
    void On<T>(string method, Action<T> handler);
    void On<T1, T2>(string method, Action<T1, T2> handler);
    
    event Action? Connected;
    event Action? Disconnected;
}
