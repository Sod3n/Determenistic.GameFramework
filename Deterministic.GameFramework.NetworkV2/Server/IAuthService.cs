using System;
using System.Threading.Tasks;

namespace Deterministic.GameFramework.NetworkV2.Server;

public interface IAuthService
{
    Task<Guid> AuthenticateAsync(string connectionId, string? token);
}
