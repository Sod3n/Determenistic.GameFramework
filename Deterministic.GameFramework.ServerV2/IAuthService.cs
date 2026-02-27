using System;
using System.Threading.Tasks;

namespace Deterministic.GameFramework.ServerV2;

/// <summary>
/// Abstraction for player authentication.
/// Implement this interface to provide custom authentication logic (JWT, OAuth, etc.)
/// </summary>
public interface IAuthService
{
    Task<Guid> AuthenticateAsync(string connectionId, string? authToken = null);
}
