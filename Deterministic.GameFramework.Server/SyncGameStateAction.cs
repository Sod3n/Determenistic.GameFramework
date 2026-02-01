using Deterministic.GameFramework.Core.Domain;

namespace Deterministic.GameFramework.Server;

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
        
        // Step 2: Replay all actions from history to rebuild game state
        var executor = new NetworkActionExecutor(gameState.Registry);
        foreach (var action in History)
        {
            action.Execute(gameState);
        }
    }
}
