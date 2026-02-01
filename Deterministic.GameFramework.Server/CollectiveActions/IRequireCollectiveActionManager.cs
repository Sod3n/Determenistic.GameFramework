using Deterministic.GameFramework.Core;

namespace Deterministic.GameFramework.Server.CollectiveActions;

public interface IRequireCollectiveActionManager : IDARAction
{
    CollectiveActionManager? CollectiveActionManager { get; set; }
}
