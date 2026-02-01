using Microsoft.AspNetCore.SignalR;

namespace Deterministic.GameFramework.Server;

/// <summary>
/// Maps SignalR connections to user IDs based on the userId query parameter.
/// This enables Clients.User() filtering in hubs.
/// </summary>
public class UserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        // Extract userId from query string (set during connection)
        return connection.GetHttpContext()?.Request.Query["userId"];
    }
}
