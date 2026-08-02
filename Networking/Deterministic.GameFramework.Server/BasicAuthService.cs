using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Deterministic.GameFramework.Network.Server;

namespace Deterministic.GameFramework.ServerV2;

/// <summary>
/// Basic authentication service that uses ConnectionId as player identity.
/// This is suitable for examples and local development.
/// For production, implement IAuthService with proper JWT/OAuth validation.
/// </summary>
public class BasicAuthService : IAuthService
{
    public Task<Guid> AuthenticateAsync(string connectionId, string? authToken = null)
    {
        // Honor a client-supplied GUID token so the same player can keep their identity
        // across reconnects (and across offline → online publish). This is the local-dev
        // stub; production should validate a JWT/OAuth token instead.
        if (!string.IsNullOrWhiteSpace(authToken) && Guid.TryParse(authToken, out var supplied))
            return Task.FromResult(supplied);

        // Fallback: deterministic Guid from ConnectionId.
        // Same connection always gets the same player ID.
        var playerId = CreateDeterministicGuid(connectionId);
        return Task.FromResult(playerId);
    }

    private static Guid CreateDeterministicGuid(string input)
    {
        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
        return new Guid(hash);
    }
}
