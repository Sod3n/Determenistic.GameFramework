using Deterministic.GameFramework.Core;

namespace Deterministic.GameFramework.Network.CollectiveActions;

public interface IRequireCollectiveActionManager : IDARAction
{
    CollectiveActionManager? CollectiveActionManager { get; set; }
}
