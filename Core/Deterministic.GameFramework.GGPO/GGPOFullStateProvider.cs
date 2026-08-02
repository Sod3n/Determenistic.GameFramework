using Deterministic.GameFramework.Common;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Network.Server;
using Deterministic.GameFramework.Serialization;

namespace Deterministic.GameFramework.GGPO;

public class GGPOFullStateProvider : IFullStateProvider
{
    private readonly StateHistory _history;

    public GGPOFullStateProvider(StateHistory history)
    {
        _history = history;
    }

    public byte[] GetFullState(Match match, out long stateTick, out long serverTick)
    {
        serverTick = match.Loop.CurrentTick;

        long confirmedTick = serverTick - GameSimulation.ConfirmationWindowTicks;
        if (confirmedTick > 0
            && _history.TryGetSnapshotData(confirmedTick, out byte[]? historyData))
        {
            stateTick = confirmedTick;
            return historyData!;
        }

        stateTick = serverTick;
        return StateSerializer.Serialize(match.State);
    }
}
