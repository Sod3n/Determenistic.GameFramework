using Deterministic.GameFramework.Network.Server;

namespace Deterministic.GameFramework.Network.Server;

public interface IFullStateProvider
{
    byte[] GetFullState(Match match, out long stateTick, out long serverTick);
}
