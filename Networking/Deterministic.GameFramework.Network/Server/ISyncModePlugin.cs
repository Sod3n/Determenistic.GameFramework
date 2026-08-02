using Deterministic.GameFramework.Common;

namespace Deterministic.GameFramework.Network.Server;

public interface ISyncModePlugin
{
    void OnMatchCreated(Match match, GameSimulation sim, INetworkServer server);
}
