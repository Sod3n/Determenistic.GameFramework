using System.Diagnostics;
using Deterministic.GameFramework.Server;
using Deterministic.GameFramework.Network;
using Deterministic.GameFramework.Core;
using Deterministic.GameFramework.Core.Utils;

namespace Deterministic.GameFramework.Examples;

/// <summary>
/// Demonstrates player reconnection and history replay validation.
/// Shows how determinism validation can verify that replaying action history
/// produces identical game state - critical for reconnecting players or spectators.
/// </summary>
public class DeterminismValidation_Reconnection
{
    public static void Example()
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║     DETERMINISM VALIDATION - RECONNECTION & REPLAY           ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝\n");
        
        Console.WriteLine("This example demonstrates:");
        Console.WriteLine("  1. Server runs a game session with multiple actions");
        Console.WriteLine("  2. New client connects mid-session");
        Console.WriteLine("  3. Client replays action history to catch up");
        Console.WriteLine("  4. Determinism validation verifies states match\n");
        
        var stopwatch = Stopwatch.StartNew();
        
        // ═══════════════════════════════════════════════════════════
        // PHASE 1: Server runs game session
        // ═══════════════════════════════════════════════════════════
        Console.WriteLine("─────────────────────────────────────────────────────────────");
        Console.WriteLine("[PHASE 1] Server: Running game session");
        Console.WriteLine("─────────────────────────────────────────────────────────────");
        
        var matchId = Guid.NewGuid();
        var player1Id = Guid.NewGuid();
        var player2Id = Guid.NewGuid();
        
        // Create server game state
        var serverState = new ReconnectionGameState(matchId);
        var serverExecutor = new NetworkActionExecutor(serverState.Registry);
        
        Console.WriteLine($"✓ Server state created (Match: {matchId})");
        Console.WriteLine($"  Initial seed: {serverState.RandomProviderDomain.Seed}");
        Console.WriteLine($"  Initial counter: {serverState.Counter.Value}");
        Console.WriteLine($"  Initial score: {serverState.Score.Value}\n");
        
        // Execute several actions on server
        var actionCount = 10;
        var random = new Random(42); // Deterministic for demo
        
        Console.WriteLine($"Executing {actionCount} actions on server...");
        for (int i = 0; i < actionCount; i++)
        {
            var playerId = random.Next(2) == 0 ? player1Id : player2Id;
            var actionType = random.Next(3);
            
            INetworkAction action = actionType switch
            {
                0 => new IncrementCounterAction { DomainId = 0, ExecutorId = playerId },
                1 => new AddScoreAction { DomainId = 0, ExecutorId = playerId, Points = random.Next(1, 10) },
                _ => new RandomEventAction { DomainId = 0, ExecutorId = playerId }
            };
            
            serverExecutor.ExecuteAction(action, playerId);
            
            if ((i + 1) % 5 == 0)
            {
                Console.WriteLine($"  Progress: {i + 1}/{actionCount} actions");
            }
        }
        
        Console.WriteLine($"\n✓ Server executed {actionCount} actions");
        Console.WriteLine($"  Final counter: {serverState.Counter.Value}");
        Console.WriteLine($"  Final score: {serverState.Score.Value}");
        Console.WriteLine($"  History size: {serverState.History.Count} actions\n");
        
        // ═══════════════════════════════════════════════════════════
        // PHASE 2: New client connects and replays history
        // ═══════════════════════════════════════════════════════════
        Console.WriteLine("─────────────────────────────────────────────────────────────");
        Console.WriteLine("[PHASE 2] Client: Connecting and replaying history");
        Console.WriteLine("─────────────────────────────────────────────────────────────");
        
        // Create fresh client state (simulates reconnecting player)
        var clientState = new ReconnectionGameState(matchId);
        var clientExecutor = new NetworkActionExecutor(clientState.Registry);
        
        Console.WriteLine($"✓ Client state created (fresh instance)");
        Console.WriteLine($"  Initial seed: {clientState.RandomProviderDomain.Seed}");
        Console.WriteLine($"  Initial counter: {clientState.Counter.Value}");
        Console.WriteLine($"  Initial score: {clientState.Score.Value}\n");
        
        // Client receives and replays history
        Console.WriteLine($"Replaying {serverState.History.Count} actions from history...");
        
        var replayStopwatch = Stopwatch.StartNew();
        foreach (var action in serverState.History)
        {
            // Clone action via JSON (simulates network transmission)
            var actionJson = JsonSerializer.ToJson(action);
            var clonedAction = JsonSerializer.FromJson<INetworkAction>(actionJson);
            
            clientExecutor.ExecuteAction(clonedAction, clonedAction.ExecutorId);
        }
        replayStopwatch.Stop();
        
        Console.WriteLine($"✓ Client replayed all {serverState.History.Count} actions");
        Console.WriteLine($"  Replay time: {replayStopwatch.ElapsedMilliseconds} ms");
        Console.WriteLine($"  Avg per action: {(double)replayStopwatch.ElapsedMilliseconds / serverState.History.Count:F2} ms\n");
        
        // ═══════════════════════════════════════════════════════════
        // PHASE 3: Validate states match using determinism validator
        // ═══════════════════════════════════════════════════════════
        Console.WriteLine("─────────────────────────────────────────────────────────────");
        Console.WriteLine("[PHASE 3] Validation: Comparing server and client states");
        Console.WriteLine("─────────────────────────────────────────────────────────────");
        
        // Enable validation
        DeterminismValidator<ReconnectionGameState>.IsEnabled = true;
        DeterminismValidator<ReconnectionGameState>.SkipStateValidation = false; // Full validation
        DeterminismValidator<ReconnectionGameState>.UseFastStateComparison = true;
        
        // Create validator to compare states
        var validator = new DeterminismValidator<ReconnectionGameState>(serverState, clientState, matchId);
        
        Console.WriteLine("Comparing game states...");
        
        // Compare by executing a no-op action on both
        var syncAction = new SyncCheckAction { DomainId = 0, ExecutorId = player1Id };
        var isValid = validator.ValidateAction(
            syncAction,
            () => serverExecutor.ExecuteAction(syncAction, player1Id)
        );
        
        stopwatch.Stop();
        
        // ═══════════════════════════════════════════════════════════
        // PHASE 4: Report results
        // ═══════════════════════════════════════════════════════════
        Console.WriteLine("\n╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                      VALIDATION RESULTS                      ║");
        Console.WriteLine("╠══════════════════════════════════════════════════════════════╣");
        
        if (isValid)
        {
            Console.WriteLine("║ ✅ SUCCESS: States match perfectly!                          ║");
            Console.WriteLine("╠══════════════════════════════════════════════════════════════╣");
            Console.WriteLine($"║ Server counter:  {serverState.Counter.Value,4}                                       ║");
            Console.WriteLine($"║ Client counter:  {clientState.Counter.Value,4}                                       ║");
            Console.WriteLine($"║ Server score:    {serverState.Score.Value,4}                                       ║");
            Console.WriteLine($"║ Client score:    {clientState.Score.Value,4}                                       ║");
            Console.WriteLine("╠══════════════════════════════════════════════════════════════╣");
            Console.WriteLine($"║ Actions replayed:        {serverState.History.Count,4}                              ║");
            Console.WriteLine($"║ Total time:              {stopwatch.ElapsedMilliseconds,6} ms                          ║");
            Console.WriteLine($"║ Replay time:             {replayStopwatch.ElapsedMilliseconds,6} ms                          ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
            
            Console.WriteLine("\n💡 What this proves:");
            Console.WriteLine("   • Action history replay is deterministic");
            Console.WriteLine("   • Reconnecting players can sync perfectly");
            Console.WriteLine("   • Spectators can join mid-game and catch up");
            Console.WriteLine("   • Game replays will show exact same outcome");
        }
        else
        {
            Console.WriteLine("║ ❌ FAILURE: States diverged during replay!                  ║");
            Console.WriteLine("╠══════════════════════════════════════════════════════════════╣");
            Console.WriteLine($"║ Server counter:  {serverState.Counter.Value,4}                                       ║");
            Console.WriteLine($"║ Client counter:  {clientState.Counter.Value,4}                                       ║");
            Console.WriteLine($"║ Server score:    {serverState.Score.Value,4}                                       ║");
            Console.WriteLine($"║ Client score:    {clientState.Score.Value,4}                                       ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
            
            Console.WriteLine("\n⚠️  Possible causes:");
            Console.WriteLine("   • Non-deterministic random number generation");
            Console.WriteLine("   • Time-based logic (DateTime.Now, timestamps)");
            Console.WriteLine("   • External state dependencies");
            Console.WriteLine("   • Incorrect action serialization");
        }
        
        Console.WriteLine("\n=== Example Complete ===\n");
    }
    
    /// <summary>
    /// Demonstrates spectator joining mid-game
    /// </summary>
    public static void SpectatorExample()
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║        DETERMINISM VALIDATION - SPECTATOR JOIN               ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝\n");
        
        var matchId = Guid.NewGuid();
        var player1Id = Guid.NewGuid();
        var player2Id = Guid.NewGuid();
        var spectatorId = Guid.NewGuid();
        
        // Server runs game
        Console.WriteLine("[Server] Running game...");
        var serverState = new ReconnectionGameState(matchId);
        var serverExecutor = new NetworkActionExecutor(serverState.Registry);
        
        // Execute 20 actions
        for (int i = 0; i < 20; i++)
        {
            var action = new IncrementCounterAction 
            { 
                DomainId = 0, 
                ExecutorId = i % 2 == 0 ? player1Id : player2Id 
            };
            serverExecutor.ExecuteAction(action, action.ExecutorId);
        }
        
        Console.WriteLine($"✓ Server at action #{serverState.History.Count}");
        Console.WriteLine($"  Counter: {serverState.Counter.Value}, Score: {serverState.Score.Value}\n");
        
        // Spectator joins
        Console.WriteLine($"[Spectator {spectatorId}] Joining game...");
        var spectatorState = new ReconnectionGameState(matchId);
        var spectatorExecutor = new NetworkActionExecutor(spectatorState.Registry);
        
        // Replay history
        foreach (var action in serverState.History)
        {
            var actionJson = JsonSerializer.ToJson(action);
            var clonedAction = JsonSerializer.FromJson<INetworkAction>(actionJson);
            spectatorExecutor.ExecuteAction(clonedAction, clonedAction.ExecutorId);
        }
        
        Console.WriteLine($"✓ Spectator synced to action #{spectatorState.History.Count}");
        Console.WriteLine($"  Counter: {spectatorState.Counter.Value}, Score: {spectatorState.Score.Value}\n");
        
        // Validate
        DeterminismValidator<ReconnectionGameState>.IsEnabled = true;
        var validator = new DeterminismValidator<ReconnectionGameState>(serverState, spectatorState, matchId);
        
        var syncAction = new SyncCheckAction { DomainId = 0, ExecutorId = spectatorId };
        var isValid = validator.ValidateAction(
            syncAction,
            () => serverExecutor.ExecuteAction(syncAction, spectatorId)
        );
        
        if (isValid)
        {
            Console.WriteLine("✅ Spectator successfully synced with server!");
            Console.WriteLine("   Can now observe game in real-time\n");
        }
        else
        {
            Console.WriteLine("❌ Spectator state diverged from server!\n");
        }
    }
}

// ═══════════════════════════════════════════════════════════════════
// Game State and Actions for Reconnection Example
// ═══════════════════════════════════════════════════════════════════

public class ReconnectionGameState : NetworkGameState
{
    public ObservableAttribute<int> Counter { get; }
    public ObservableAttribute<int> Score { get; }
    
    public ReconnectionGameState(Guid matchId) : base(matchId, matchId.GetHashCode())
    {
        Counter = new ObservableAttribute<int>(0);
        Score = new ObservableAttribute<int>(0);
    }
}

public class ReconnectionGameStateFactory : IGameStateFactory<Guid, ReconnectionGameState>
{
    public ReconnectionGameState CreateGameState(Guid matchId) => new ReconnectionGameState(matchId);
}

public class IncrementCounterAction : NetworkAction<ReconnectionGameState, IncrementCounterAction>
{
    protected override void ExecuteProcess(ReconnectionGameState domain)
    {
        domain.Counter.Value++;
    }
}

public class AddScoreAction : NetworkAction<ReconnectionGameState, AddScoreAction>
{
    public int Points { get; set; }
    
    protected override void ExecuteProcess(ReconnectionGameState domain)
    {
        domain.Score.Value += Points;
    }
}

public class RandomEventAction : NetworkAction<ReconnectionGameState, RandomEventAction>
{
    protected override void ExecuteProcess(ReconnectionGameState domain)
    {
        // Uses deterministic random from RandomProviderDomain
        var randomValue = domain.RandomProviderDomain.Random.Next(1, 100);
        
        if (randomValue > 50)
        {
            domain.Score.Value += randomValue;
        }
        else
        {
            domain.Counter.Value++;
        }
    }
}

public class SyncCheckAction : NetworkAction<ReconnectionGameState, SyncCheckAction>
{
    protected override void ExecuteProcess(ReconnectionGameState domain)
    {
        // No-op action used for validation sync check
    }
}
