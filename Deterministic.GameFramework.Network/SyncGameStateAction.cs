using Deterministic.GameFramework.Core.Domain;

namespace Deterministic.GameFramework.Network;

/// <summary>
/// Generic action to synchronize full game state from server to client.
/// Sent on initial connection, reconnection, or when client needs full state refresh.
/// 
/// Uses history replay to rebuild state deterministically.
/// </summary>
public class SyncGameStateAction<TGameState> : NetworkAction<TGameState, SyncGameStateAction<TGameState>> 
    where TGameState : NetworkGameState
{
    public List<INetworkAction> History { get; set; } = new();
    public int Seed { get; set; }

    // Use Main thread for state sync
    public override NetworkThread Thread => NetworkThread.Main;

    // Parameterless constructor for deserialization
    public SyncGameStateAction()
    {
    }

    // Constructor for creating from GameState
    public SyncGameStateAction(TGameState state)
    {
        History = state.History;
        Seed = state.RandomProviderDomain.Seed;
    }

    protected override void ExecuteProcess(TGameState gameState)
    {
        // Step 1: Restore the random seed to ensure deterministic behavior
        gameState.RandomProviderDomain.Reset(Seed);
        
        // Step 2: Find the GameLoop in the domain tree
        var gameLoop = gameState.GetInParent<GameLoop>();
        if (gameLoop == null)
        {
            // Fallback: replay without tick advancement (no game loop available)
            foreach (var action in History)
            {
                action.Execute(gameState);
            }
            return;
        }
        
        // Step 3: Schedule each history action on its stamped tick
        foreach (var action in History)
        {
            var tick = action.Tick;
            if (tick > 0)
            {
                gameLoop.ScheduleOnTick(tick, () => action.Execute(gameState));
            }
            else
            {
                // Actions without a tick (e.g. setup actions) execute immediately
                action.Execute(gameState);
            }
        }
        
        // Step 4: Find the highest tick in history and advance the game loop to it
        long maxTick = 0;
        foreach (var action in History)
        {
            if (action.Tick > maxTick) maxTick = action.Tick;
        }
        
        if (maxTick > 0)
        {
            gameLoop.AdvanceToTick(maxTick);
        }
    }
}
