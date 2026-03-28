using System;
using System.Threading.Tasks;

namespace Deterministic.GameFramework.Network.Server;

public interface IAuthService
{
    Task<Guid> AuthenticateAsync(string connectionId, string? token);
}
