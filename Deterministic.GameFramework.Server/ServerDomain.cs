using System;
using Deterministic.GameFramework.Core.Domain;
using Deterministic.GameFramework.Core.Utils;

namespace Deterministic.GameFramework.Server;

public class ServerDomain : RootDomain
{
	public static ServerDomain Instance { get; private set; } = null!;
	
	public Guid ServerId { get; private set; } = Guid.NewGuid();
	// Global network sync manager (monitors all GameStates)
	public NetworkSyncManager NetworkSyncManager { get; }
	public GameLoop GameLoop { get; }
	
	// Configuration: if true, server only relays actions without simulating
	public bool RelayOnlyMode { get; set; } = false;
	
	public ServerDomain()
	{
		Instance = this;
		// Create and attach global NetworkSyncManager as a subdomain
		NetworkSyncManager = new NetworkSyncManager(this);
		new NetworkSyncManager.CollectNetworkActionsReaction(NetworkSyncManager, this).AddTo(Disposables);
		
		// Create game loop bound to this domain
		GameLoop = new GameLoop(this);
		GameLoop.SetTargetFps(60);
		_ = GameLoop.Start(); // Start the loop so scheduled actions execute
		
		new Reaction<BranchDomain, INetworkAction>(this)
			.Prepare((_, action) =>
			{
				action.ExecutorId ??= ServerId;
				action.IsServer = true;
			})
			.AddTo(Disposables);
	}
}