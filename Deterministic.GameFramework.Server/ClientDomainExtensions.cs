using Microsoft.AspNetCore.SignalR.Client;
using Deterministic.GameFramework.Core.Utils;

namespace Deterministic.GameFramework.Server;

/// <summary>
/// Extension methods to simplify client setup.
/// </summary>
public static class ClientDomainExtensions
{
    /// <summary>
    /// Connects the client to a SignalR server and wires up all networking automatically.
    /// Handles sending actions to server and receiving actions from server.
    /// </summary>
    public static async Task<HubConnection> ConnectToServer<TGameState>(
        this ClientDomain<TGameState> client,
        string serverUrl)
        where TGameState : NetworkGameState
    {
        // Build connection URL with userId and matchId as query parameters
        var url = $"{serverUrl}?userId={client.UserId}&matchId={client.MatchId}";
        
        var connection = new HubConnectionBuilder()
            .WithUrl(url)
            .Build();
        
        // Wire up NetworkSyncManager to send actions via SignalR
        client.NetworkSyncManager.OnSync += async (matchId, actions) =>
        {
            var json = JsonSerializer.ToJson(actions);
            await connection.InvokeAsync("SyncActions", json);
        };
        
        // Handle incoming actions from server
        connection.On<string>("SyncActions", actionsJson =>
        {
            var executor = new NetworkActionExecutor(client.GameState.Registry);
            executor.ExecuteBatch(actionsJson);
        });
        
        // Connect to server
        await connection.StartAsync();
        
        return connection;
    }
}
