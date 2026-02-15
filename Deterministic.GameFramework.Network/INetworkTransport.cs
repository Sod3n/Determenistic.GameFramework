using System;
using System.Threading.Tasks;

namespace Deterministic.GameFramework.Network;

/// <summary>
/// Abstraction for network transport layer.
/// Implement this interface for each engine/platform:
/// - Unity: Use Unity-compatible SignalR client or custom WebSocket
/// - Godot: Use Godot's networking
/// - ASP.NET: Use Microsoft.AspNetCore.SignalR.Client
/// </summary>
public interface INetworkTransport
{
    /// <summary>
    /// Connect to the server.
    /// </summary>
    Task ConnectAsync();
    
    /// <summary>
    /// Disconnect from the server.
    /// </summary>
    Task DisconnectAsync();
    
    /// <summary>
    /// Send a message to the server.
    /// </summary>
    /// <param name="method">The hub method name to invoke</param>
    /// <param name="json">The JSON payload</param>
    void Send(string method, string json);
    
    /// <summary>
    /// Register a handler for messages from the server.
    /// </summary>
    /// <param name="method">The hub method name to listen for</param>
    /// <param name="handler">Callback receiving the JSON payload</param>
    void On(string method, Action<string> handler);
    
    /// <summary>
    /// Fired when the connection is established.
    /// </summary>
    event Action? Connected;
    
    /// <summary>
    /// Fired when the connection is lost.
    /// </summary>
    event Action? Disconnected;
}
