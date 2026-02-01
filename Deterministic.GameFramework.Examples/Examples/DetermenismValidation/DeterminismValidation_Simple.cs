using Deterministic.GameFramework.Server;
using Deterministic.GameFramework.Core;

namespace Deterministic.GameFramework.Examples;

/// <summary>
/// Simple example: Enable determinism validation for a single match.
/// This catches desyncs immediately when they occur.
/// </summary>
public class DeterminismValidation_Simple
{
    public static void Example()
    {
        Console.WriteLine("=== Simple Determinism Validation Example ===\n");
        
        // Step 1: Enable validation globally
        DeterminismValidator<MyGameState>.IsEnabled = true;
        Console.WriteLine("✓ Determinism validation enabled");
        
        // Step 2: Create server domain and match manager
        var serverDomain = new ServerDomain();
        var gameStateFactory = new MyGameStateFactory();
        var matchManager = new MatchManager<MyGameState>(serverDomain, gameStateFactory);
        
        // Step 3: Wrap with validating manager
        var validatingManager = new DeterminismValidatingMatchManager<MyGameState>(
            matchManager, 
            gameStateFactory
        );
        Console.WriteLine("✓ Validating match manager created");
        
        // Step 4: Create a match (shadow will be created automatically)
        var matchId = Guid.NewGuid();
        var player1Id = Guid.NewGuid();
        var player2Id = Guid.NewGuid();
        
        var gameState = matchManager.CreateMatch(matchId);
        Console.WriteLine($"✓ Match created: {matchId}");
        Console.WriteLine($"  Validator active: {validatingManager.GetValidator(matchId) != null}");
        
        // Step 5: Execute actions with validation
        var executor = new NetworkActionExecutor(gameState.Registry);
        
        // Install validation hook
        validatingManager.InstallValidationHook(matchId, executor);
        Console.WriteLine("✓ Validation hook installed on executor");
        
        // Step 6: Execute some actions
        Console.WriteLine("\nExecuting actions...");
        
        var action1 = new MyTestAction { DomainId = 0, ExecutorId = player1Id };
        executor.ExecuteAction(action1, player1Id);
        Console.WriteLine("  Action 1 executed - validation passed ✓");
        
        var action2 = new MyTestAction { DomainId = 0, ExecutorId = player2Id };
        executor.ExecuteAction(action2, player2Id);
        Console.WriteLine("  Action 2 executed - validation passed ✓");
        
        // Step 7: Check validator status
        var validator = validatingManager.GetValidator(matchId);
        if (validator != null)
        {
            Console.WriteLine($"\n✓ Validation Summary:");
            Console.WriteLine($"  Actions validated: {validator.ActionCount}");
            Console.WriteLine($"  Has failed: {validator.HasFailed}");
        }
        
        // Step 8: Cleanup
        validatingManager.OnMatchRemoved(matchId);
        matchManager.RemoveMatch(matchId);
        Console.WriteLine("\n✓ Match and validator cleaned up");
    }
}

// Example game state and action
public class MyGameState : NetworkGameState
{
    public MyGameState(Guid matchId) : base(matchId, matchId.GetHashCode()) { }
}

public class MyGameStateFactory : IGameStateFactory<MyGameState>
{
    public MyGameState CreateGameState(Guid matchId) => new MyGameState(matchId);
}

public class MyTestAction : NetworkAction<MyGameState, MyTestAction>
{
    protected override void ExecuteProcess(MyGameState domain)
    {
        Console.WriteLine($"    [MyTestAction] Executed by {ExecutorId}");
    }
}
