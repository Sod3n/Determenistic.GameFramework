using System.Collections.Generic;

namespace Deterministic.GameFramework.Network.CollectiveActions;

public interface ICollectiveAction : INetworkAction
{
    string CollectiveKey { get; }
}

public interface IWaitForAllAction : ICollectiveAction
{
}

public interface IVoteForAction : ICollectiveAction
{
    string VoteOption { get; }
    void TriggerWinningExecution(Dictionary<string, int> voteCounts);
}
