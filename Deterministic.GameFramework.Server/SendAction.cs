using Deterministic.GameFramework.Core;
using Deterministic.GameFramework.Core.Domain;

namespace Deterministic.GameFramework.Server;

/// <summary>
/// Generic action to send a network action to the server.
/// Used by clients to queue actions for network transmission.
/// ExecutorId should be set by the caller before creating this action.
/// </summary>
public class SendAction(INetworkAction action, LeafDomain target) : DARAction<RootDomain, SendAction>
{
    protected override void ExecuteProcess(RootDomain domain)
    {
        // Find NetworkSyncManager in the domain tree
        var networkSyncManager = domain.GetFirst<NetworkSyncManager>();
        if (networkSyncManager == null)
        {
            throw new InvalidOperationException("NetworkSyncManager not found in domain tree");
        }
        
        networkSyncManager.AddPendingAction(target, action);
    }
}
