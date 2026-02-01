using System.Diagnostics;
using Deterministic.GameFramework.Server;
using Deterministic.GameFramework.Core;

namespace Deterministic.GameFramework.Examples;

/// <summary>
/// Complex example: Debugging a desync that appears randomly after many actions.
/// Shows how to use execution logs to identify the exact point of divergence.
/// </summary>
public class DeterminismValidation_Complex
{
    public static void Example()
    {
        Console.WriteLine("=== Complex Determinism Validation Example ===");
        Console.WriteLine("Simulating a desync that appears randomly after many actions\n");
        
        // Step 1: Enable validation and subscribe to failure events
        DeterminismValidator<ComplexGameState>.IsEnabled = true;
        
        DeterminismValidator<ComplexGameState>.OnDeterminismFailure += 
            (matchId, actionType, actionJson, primaryLog, shadowLog, mismatchIndex) =>
        {
            Console.WriteLine("\n🔴 DESYNC DETECTED - Detailed Analysis:");
            Console.WriteLine($"   Match: {matchId}");
            Console.WriteLine($"   Failed on action: {actionType}");
            Console.WriteLine($"   Mismatch at execution event #{mismatchIndex}");
            
            // Save logs to files for detailed analysis
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var logDir = $"desync_logs/{matchId}_{timestamp}";
            Directory.CreateDirectory(logDir);
            
            File.WriteAllText($"{logDir}/action.json", actionJson);
            File.WriteAllText($"{logDir}/primary_log.txt", primaryLog);
            File.WriteAllText($"{logDir}/shadow_log.txt", shadowLog);
            
            Console.WriteLine($"   Logs saved to: {logDir}");
            
            // Parse logs to find the divergence point
            var primaryLines = primaryLog.Split('\n');
            var shadowLines = shadowLog.Split('\n');
            
            if (mismatchIndex < primaryLines.Length && mismatchIndex < shadowLines.Length)
            {
                Console.WriteLine($"\n   Primary event: {primaryLines[mismatchIndex]}");
                Console.WriteLine($"   Shadow event:  {shadowLines[mismatchIndex]}");
            }
        };
        
        Console.WriteLine("✓ Validation enabled with failure handler");
        
        // Step 2: Setup
        var serverDomain = new ServerDomain();
        var gameStateFactory = new ComplexGameStateFactory();
        var matchManager = new MatchManager<ComplexGameState>(serverDomain, gameStateFactory);
        var validatingManager = new DeterminismValidatingMatchManager<ComplexGameState>(
            matchManager, 
            gameStateFactory
        );
        
        // Step 3: Create match
        var matchId = Guid.NewGuid();
        var player1Id = Guid.NewGuid();
        var player2Id = Guid.NewGuid();
        
        var gameState = matchManager.CreateMatch(matchId);
        var executor = new NetworkActionExecutor(gameState.Registry);
        validatingManager.InstallValidationHook(matchId, executor);
        
        Console.WriteLine($"✓ Match created: {matchId}");
        
        // Step 4: Simulate many actions (desync will occur randomly)
        Console.WriteLine("\nExecuting 100 actions...");
        
        var stopwatch = Stopwatch.StartNew();
        var random = new Random(42); // Deterministic for demo
        var actionCount = 0;
        var desyncDetected = false;
        
        for (int i = 0; i < 100 && !desyncDetected; i++)
        {
            var playerId = random.Next(2) == 0 ? player1Id : player2Id;
            
            // Randomly choose action type
            INetworkAction action = random.Next(3) switch
            {
                0 => new ComplexAction1 { DomainId = 0, ExecutorId = playerId },
                1 => new ComplexAction2 { DomainId = 0, ExecutorId = playerId },
                _ => new ComplexAction3 { DomainId = 0, ExecutorId = playerId }
            };
            
            try
            {
                var success = executor.ExecuteAction(action, playerId);
                if (!success)
                {
                    desyncDetected = true;
                    Console.WriteLine($"\n❌ Desync detected at action #{i + 1}");
                    break;
                }
                
                actionCount++;
                
                if ((i + 1) % 20 == 0)
                {
                    Console.WriteLine($"  Progress: {i + 1}/100 actions executed");
                }
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("DETERMINISM FAILURE"))
            {
                desyncDetected = true;
                Console.WriteLine($"\n❌ Desync detected at action #{i + 1}");
                Console.WriteLine($"   Exception: {ex.Message}");
                break;
            }
        }
        
        stopwatch.Stop();
        
        // Step 5: Report results
        var validator = validatingManager.GetValidator(matchId);
        if (validator != null)
        {
            Console.WriteLine($"\n📊 Validation Summary:");
            Console.WriteLine($"   Total actions executed: {actionCount}");
            Console.WriteLine($"   Actions validated: {validator.ActionCount}");
            Console.WriteLine($"   Desync detected: {validator.HasFailed}");
            Console.WriteLine($"   Total time: {stopwatch.ElapsedMilliseconds} ms");
            Console.WriteLine($"   Avg per action: {(double)stopwatch.ElapsedMilliseconds / actionCount:F2} ms");
            
            if (!validator.HasFailed)
            {
                Console.WriteLine($"\n✅ All {actionCount} actions executed deterministically!");
            }
        }
        
        // Step 6: Cleanup
        validatingManager.OnMatchRemoved(matchId);
        matchManager.RemoveMatch(matchId);
        
        Console.WriteLine("\n=== Example Complete ===");
    }
    
    /// <summary>
    /// Example of how to debug a specific desync scenario.
    /// This simulates a bug where reactions fire in different order.
    /// </summary>
    public static void DebugSpecificDesync()
    {
        Console.WriteLine("\n=== Debugging Specific Desync Scenario ===");
        Console.WriteLine("Simulating a bug where reactions fire in non-deterministic order\n");
        
        DeterminismValidator<ComplexGameState>.IsEnabled = true;
        
        // This will catch the exact moment when reactions diverge
        DeterminismValidator<ComplexGameState>.OnDeterminismFailure += 
            (matchId, actionType, actionJson, primaryLog, shadowLog, mismatchIndex) =>
        {
            Console.WriteLine("\n🔍 Analyzing desync...");
            
            var primaryEvents = primaryLog.Split('\n');
            var shadowEvents = shadowLog.Split('\n');
            
            // Show 5 events before and after the mismatch
            var contextStart = Math.Max(0, mismatchIndex - 5);
            var contextEnd = Math.Min(Math.Max(primaryEvents.Length, shadowEvents.Length), mismatchIndex + 5);
            
            Console.WriteLine("\n📋 Execution context:");
            for (int i = contextStart; i < contextEnd; i++)
            {
                var marker = i == mismatchIndex ? ">>> " : "    ";
                
                if (i < primaryEvents.Length)
                    Console.WriteLine($"{marker}P: {primaryEvents[i]}");
                else
                    Console.WriteLine($"{marker}P: [MISSING]");
                    
                if (i < shadowEvents.Length)
                    Console.WriteLine($"{marker}S: {shadowEvents[i]}");
                else
                    Console.WriteLine($"{marker}S: [MISSING]");
                    
                if (i == mismatchIndex)
                    Console.WriteLine("    ^^^ DIVERGENCE POINT ^^^");
                    
                Console.WriteLine();
            }
            
            // Provide debugging hints
            Console.WriteLine("💡 Debugging hints:");
            if (mismatchIndex < primaryEvents.Length && mismatchIndex < shadowEvents.Length)
            {
                var primaryEvent = primaryEvents[mismatchIndex];
                var shadowEvent = shadowEvents[mismatchIndex];
                
                if (primaryEvent.Contains("Reaction") && shadowEvent.Contains("Reaction"))
                {
                    Console.WriteLine("   - Different reactions fired at the same point");
                    Console.WriteLine("   - Check reaction registration order in domain constructors");
                    Console.WriteLine("   - Ensure reactions are registered deterministically");
                }
                else if (primaryEvent.Contains("Action") != shadowEvent.Contains("Action"))
                {
                    Console.WriteLine("   - Action execution diverged");
                    Console.WriteLine("   - Check for conditional logic that depends on non-deterministic state");
                }
            }
            else
            {
                Console.WriteLine("   - One simulation has more/fewer execution events");
                Console.WriteLine("   - Check for conditional reactions or domain creation");
                Console.WriteLine("   - Verify IdProviderDomain is used for deterministic IDs");
            }
        };
        
        // Setup and execute problematic action
        var serverDomain = new ServerDomain();
        var gameStateFactory = new ComplexGameStateFactory();
        var matchManager = new MatchManager<ComplexGameState>(serverDomain, gameStateFactory);
        var validatingManager = new DeterminismValidatingMatchManager<ComplexGameState>(
            matchManager, 
            gameStateFactory
        );
        
        var matchId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var gameState = matchManager.CreateMatch(matchId);
        var executor = new NetworkActionExecutor(gameState.Registry);
        validatingManager.InstallValidationHook(matchId, executor);
        
        Console.WriteLine("Executing problematic action...");
        
        // This action will trigger the desync
        var problematicAction = new ProblematicAction { DomainId = 0, ExecutorId = playerId };
        executor.ExecuteAction(problematicAction, playerId);
        
        validatingManager.OnMatchRemoved(matchId);
        matchManager.RemoveMatch(matchId);
    }
}

// Example game state with complex behavior
public class ComplexGameState : NetworkGameState
{
    public ObservableAttribute<int> TurnCount { get; }
    public ObservableAttribute<int> Score { get; }
    
    public ComplexGameState(Guid matchId) : base(matchId, matchId.GetHashCode())
    {
        TurnCount = new ObservableAttribute<int>(0);
        Score = new ObservableAttribute<int>(0);
        
        // Register reactions that might fire in different order
        new Reaction<ComplexGameState, ComplexAction1>(this)
            .After((domain, action) => {
                TurnCount.Value++;
                Console.WriteLine($"      [Reaction] TurnCount incremented to {TurnCount.Value}");
            })
            .AddTo(Disposables);
            
        new Reaction<ComplexGameState, ComplexAction1>(this)
            .After((domain, action) => {
                Score.Value += 10;
                Console.WriteLine($"      [Reaction] Score increased to {Score.Value}");
            })
            .AddTo(Disposables);
    }
}

public class ComplexGameStateFactory : IGameStateFactory<ComplexGameState>
{
    public ComplexGameState CreateGameState(Guid matchId) => new ComplexGameState(matchId);
}

// Example actions
public class ComplexAction1 : NetworkAction<ComplexGameState, ComplexAction1>
{
    protected override void ExecuteProcess(ComplexGameState domain)
    {
        Console.WriteLine($"      [ComplexAction1] Executed");
    }
}

public class ComplexAction2 : NetworkAction<ComplexGameState, ComplexAction2>
{
    protected override void ExecuteProcess(ComplexGameState domain)
    {
        Console.WriteLine($"      [ComplexAction2] Executed");
        domain.Score.Value += 5;
    }
}

public class ComplexAction3 : NetworkAction<ComplexGameState, ComplexAction3>
{
    protected override void ExecuteProcess(ComplexGameState domain)
    {
        Console.WriteLine($"      [ComplexAction3] Executed");
        domain.TurnCount.Value++;
    }
}

// Action that demonstrates a desync bug
public class ProblematicAction : NetworkAction<ComplexGameState, ProblematicAction>
{
    protected override void ExecuteProcess(ComplexGameState domain)
    {
        Console.WriteLine($"      [ProblematicAction] Executed");
        
        // This simulates a bug where behavior differs based on non-deterministic state
        // In reality, this might be: DateTime.Now, Random without seed, external API call, etc.
        var shouldTriggerBug = domain.GetHashCode() % 2 == 0; // Non-deterministic!
        
        if (shouldTriggerBug)
        {
            // This will only happen on one of primary/shadow
            domain.Score.Value += 100;
        }
    }
}
