using Deterministic.GameFramework.Server;
using Deterministic.GameFramework.Core;

namespace Deterministic.GameFramework.Examples;

/// <summary>
/// Examples demonstrating different types of desyncs that can be caught.
/// Each example shows a specific desync scenario and how it's detected.
/// </summary>
public class DeterminismValidation_DesyncExamples
{
    /// <summary>
    /// Example 1: Reaction order desync
    /// Two reactions registered in non-deterministic order cause different execution sequences.
    /// </summary>
    public static void Example1_ReactionOrderDesync()
    {
        Console.WriteLine("=== Example 1: Reaction Order Desync ===\n");
        
        DeterminismValidator<DesyncGameState1>.IsEnabled = true;
        
        var serverDomain = new ServerDomain();
        var factory = new DesyncGameState1Factory();
        var matchManager = new MatchManager<DesyncGameState1>(serverDomain, factory);
        var validatingManager = new DeterminismValidatingMatchManager<DesyncGameState1>(matchManager, factory);
        
        var matchId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var gameState = matchManager.CreateMatch(matchId);
        var executor = new NetworkActionExecutor(gameState.Registry);
        validatingManager.InstallValidationHook(matchId, executor);
        
        Console.WriteLine("Executing action that triggers reactions in non-deterministic order...\n");
        
        var action = new TriggerReactionsAction { DomainId = 0, ExecutorId = playerId };
        var success = executor.ExecuteAction(action, playerId);
        
        if (!success)
        {
            Console.WriteLine("\n✓ Desync caught! Validation failed.\n");
        }
        else
        {
            var validator = validatingManager.GetValidator(matchId);
            if (validator?.HasFailed == true)
            {
                Console.WriteLine("\n✓ Desync caught! Validator marked as failed.\n");
            }
            else
            {
                Console.WriteLine("\n❌ UNEXPECTED: Action succeeded without detecting desync!\n");
            }
        }
        
        validatingManager.OnMatchRemoved(matchId);
        matchManager.RemoveMatch(matchId);
    }
    
    /// <summary>
    /// Example 2: Missing reaction desync
    /// A reaction exists on primary but not on shadow (or vice versa).
    /// </summary>
    public static void Example2_MissingReactionDesync()
    {
        Console.WriteLine("=== Example 2: Missing Reaction Desync ===\n");
        
        DeterminismValidator<DesyncGameState2>.IsEnabled = true;
        
        var serverDomain = new ServerDomain();
        var factory = new DesyncGameState2Factory();
        var matchManager = new MatchManager<DesyncGameState2>(serverDomain, factory);
        var validatingManager = new DeterminismValidatingMatchManager<DesyncGameState2>(matchManager, factory);
        
        var matchId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var gameState = matchManager.CreateMatch(matchId);
        var executor = new NetworkActionExecutor(gameState.Registry);
        validatingManager.InstallValidationHook(matchId, executor);
        
        Console.WriteLine("Executing action with conditional reaction...\n");
        
        var action = new ConditionalAction { DomainId = 0, ExecutorId = playerId };
        var success = executor.ExecuteAction(action, playerId);
        
        if (!success)
        {
            Console.WriteLine("\n✓ Desync caught! Validation failed.\n");
        }
        else
        {
            var validator = validatingManager.GetValidator(matchId);
            if (validator?.HasFailed == true)
            {
                Console.WriteLine("\n✓ Desync caught! Validator marked as failed.\n");
            }
            else
            {
                Console.WriteLine("\n❌ UNEXPECTED: Action succeeded without detecting desync!\n");
            }
        }
        
        validatingManager.OnMatchRemoved(matchId);
        matchManager.RemoveMatch(matchId);
    }
    
    /// <summary>
    /// Example 3: Domain ID desync
    /// Domains created with non-deterministic IDs cause state divergence.
    /// </summary>
    public static void Example3_DomainIdDesync()
    {
        Console.WriteLine("=== Example 3: Domain ID Desync ===\n");
        
        DeterminismValidator<DesyncGameState3>.IsEnabled = true;
        
        var serverDomain = new ServerDomain();
        var factory = new DesyncGameState3Factory();
        var matchManager = new MatchManager<DesyncGameState3>(serverDomain, factory);
        var validatingManager = new DeterminismValidatingMatchManager<DesyncGameState3>(matchManager, factory);
        
        var matchId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var gameState = matchManager.CreateMatch(matchId);
        var executor = new NetworkActionExecutor(gameState.Registry);
        validatingManager.InstallValidationHook(matchId, executor);
        
        Console.WriteLine("Executing action that creates domain with non-deterministic ID...\n");
        
        var action = new CreateDomainAction { DomainId = 0, ExecutorId = playerId };
        var success = executor.ExecuteAction(action, playerId);
        
        if (!success)
        {
            Console.WriteLine("\n✓ Desync caught! Validation failed.\n");
        }
        else
        {
            var validator = validatingManager.GetValidator(matchId);
            if (validator?.HasFailed == true)
            {
                Console.WriteLine("\n✓ Desync caught! Validator marked as failed.\n");
            }
            else
            {
                Console.WriteLine("\n❌ UNEXPECTED: Action succeeded without detecting desync!\n");
            }
        }
        
        validatingManager.OnMatchRemoved(matchId);
        matchManager.RemoveMatch(matchId);
    }
    
    /// <summary>
    /// Example 4: Time-based logic desync
    /// Using DateTime.Now causes different execution paths on primary vs shadow.
    /// </summary>
    public static void Example4_TimeBasedDesync()
    {
        Console.WriteLine("=== Example 4: Time-Based Logic Desync ===\n");
        
        DeterminismValidator<DesyncGameState4>.IsEnabled = true;
        
        var serverDomain = new ServerDomain();
        var factory = new DesyncGameState4Factory();
        var matchManager = new MatchManager<DesyncGameState4>(serverDomain, factory);
        var validatingManager = new DeterminismValidatingMatchManager<DesyncGameState4>(matchManager, factory);
        
        var matchId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var gameState = matchManager.CreateMatch(matchId);
        var executor = new NetworkActionExecutor(gameState.Registry);
        validatingManager.InstallValidationHook(matchId, executor);
        
        Console.WriteLine("Executing action with time-based logic...\n");
        
        var action = new TimeBasedAction { DomainId = 0, ExecutorId = playerId };
        var success = executor.ExecuteAction(action, playerId);
        
        if (!success)
        {
            Console.WriteLine("\n✓ Desync caught! Validation failed.\n");
        }
        else
        {
            var validator = validatingManager.GetValidator(matchId);
            if (validator?.HasFailed == true)
            {
                Console.WriteLine("\n✓ Desync caught! Validator marked as failed.\n");
            }
            else
            {
                Console.WriteLine("\n❌ UNEXPECTED: Action succeeded without detecting desync!\n");
            }
        }
        
        validatingManager.OnMatchRemoved(matchId);
        matchManager.RemoveMatch(matchId);
    }
    
    public static void RunAllExamples()
    {
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║     DETERMINISM VALIDATION - DESYNC DETECTION EXAMPLES     ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝\n");
        
        const int runsPerExample = 5;
        
        Console.WriteLine($"Running each example {runsPerExample} times to ensure desync detection...\n");
        
        for (int i = 1; i <= runsPerExample; i++)
        {
            Console.WriteLine($"--- Run {i}/{runsPerExample} ---\n");
            
            Example1_ReactionOrderDesync();
            Console.WriteLine();
            
            Example2_MissingReactionDesync();
            Console.WriteLine();
            
            Example3_DomainIdDesync();
            Console.WriteLine();
            
            Example4_TimeBasedDesync();
            
            if (i < runsPerExample)
            {
                Console.WriteLine("\n" + new string('─', 60) + "\n");
            }
        }
        
        Console.WriteLine("\n╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                    ALL EXAMPLES COMPLETE                   ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
    }
}

// ============================================================================
// Example 1: Reaction Order Desync
// ============================================================================

public class DesyncGameState1 : NetworkGameState
{
    public DesyncGameState1(Guid matchId) : base(matchId, matchId.GetHashCode())
    {
        Console.WriteLine($"      [DesyncGameState1] Created for match {matchId}");
        
        // BAD: Using GetHashCode() to determine registration order
        // GetHashCode() can return different values for the same object across instances
        var orderingValue = this.GetHashCode();
        
        if (orderingValue % 2 == 0)
        {
            new ReactionA(this).AddTo(Disposables);
            new ReactionB(this).AddTo(Disposables);
            Console.WriteLine($"      [DesyncGameState1] Registered A then B (hash: {orderingValue})");
        }
        else
        {
            new ReactionB(this).AddTo(Disposables);
            new ReactionA(this).AddTo(Disposables);
            Console.WriteLine($"      [DesyncGameState1] Registered B then A (hash: {orderingValue})");
        }
    }
}

public class ReactionA : Reaction, IAfterReaction<DesyncGameState1, TriggerReactionsAction>
{
    public ReactionA(DesyncGameState1 target) : base(target) { }
    
    public void OnAfter(DesyncGameState1 domain, TriggerReactionsAction action)
    {
        Console.WriteLine("      [ReactionA] Executed");
    }
}

public class ReactionB : Reaction, IAfterReaction<DesyncGameState1, TriggerReactionsAction>
{
    public ReactionB(DesyncGameState1 target) : base(target) { }
    
    public void OnAfter(DesyncGameState1 domain, TriggerReactionsAction action)
    {
        Console.WriteLine("      [ReactionB] Executed");
    }
}

public class DesyncGameState1Factory : IGameStateFactory<DesyncGameState1>
{
    public DesyncGameState1 CreateGameState(Guid matchId) => new DesyncGameState1(matchId);
}

public class TriggerReactionsAction : NetworkAction<DesyncGameState1, TriggerReactionsAction>
{
    protected override void ExecuteProcess(DesyncGameState1 domain)
    {
        Console.WriteLine("      [TriggerReactionsAction] Executed");
    }
}

// ============================================================================
// Example 2: Missing Reaction Desync
// ============================================================================

public class DesyncGameState2 : NetworkGameState
{
    public DesyncGameState2(Guid matchId) : base(matchId, matchId.GetHashCode())
    {
        Console.WriteLine($"      [DesyncGameState2] Created for match {matchId}");
        
        // BAD: Conditional reaction registration based on Guid.NewGuid()
        // Each instance generates a different GUID, causing divergence
        var featureFlag = Guid.NewGuid().GetHashCode() % 2 == 0;
        
        if (featureFlag)
        {
            Console.WriteLine("      [DesyncGameState2] Registering conditional reaction");
            new ConditionalReaction(this).AddTo(Disposables);
        }
        else
        {
            Console.WriteLine("      [DesyncGameState2] Skipping conditional reaction");
        }
    }
}

public class ConditionalReaction : Reaction, IAfterReaction<DesyncGameState2, ConditionalAction>
{
    public ConditionalReaction(DesyncGameState2 target) : base(target) { }
    
    public void OnAfter(DesyncGameState2 domain, ConditionalAction action)
    {
        Console.WriteLine("      [ConditionalReaction] Fired");
    }
}

public class DesyncGameState2Factory : IGameStateFactory<DesyncGameState2>
{
    public DesyncGameState2 CreateGameState(Guid matchId) => new DesyncGameState2(matchId);
}

public class ConditionalAction : NetworkAction<DesyncGameState2, ConditionalAction>
{
    protected override void ExecuteProcess(DesyncGameState2 domain)
    {
        Console.WriteLine("      [ConditionalAction] Executed");
    }
}

// ============================================================================
// Example 3: Domain ID Desync
// ============================================================================

public class DesyncGameState3 : NetworkGameState
{
    public DesyncGameState3(Guid matchId) : base(matchId, matchId.GetHashCode())
    {
        Console.WriteLine($"      [DesyncGameState3] Created for match {matchId}");
        
        // Register reaction that fires based on child count
        new ChildCountReaction(this).AddTo(Disposables);
    }
}

public class ChildCountReaction : Reaction, IAfterReaction<DesyncGameState3, CreateDomainAction>
{
    public ChildCountReaction(DesyncGameState3 target) : base(target) { }
    
    public void OnAfter(DesyncGameState3 domain, CreateDomainAction action)
    {
        // This reaction fires based on child count, which differs due to IdProvider
        var childCount = domain.Subdomains.Count;
        Console.WriteLine($"      [ChildCountReaction] Child count: {childCount}");
    }
}

public class DesyncGameState3Factory : IGameStateFactory<DesyncGameState3>
{
    public DesyncGameState3 CreateGameState(Guid matchId) => new DesyncGameState3(matchId);
}

public class CreateDomainAction : NetworkAction<DesyncGameState3, CreateDomainAction>
{
    protected override void ExecuteProcess(DesyncGameState3 domain)
    {
        Console.WriteLine("      [CreateDomainAction] Creating child domain");
        
        // BAD: Using Guid.NewGuid() for domain identification
        // Each instance generates different GUIDs, causing state divergence
        var customId = Guid.NewGuid();
        var childDomain = new ChildDomainWithCustomId(domain, customId);
        domain.Subdomains.Add(childDomain);
        
        Console.WriteLine($"      Created domain with custom ID: {customId}, actual ID: {childDomain.Id}");
    }
}

public class ChildDomainWithCustomId : LeafDomain
{
    public Guid CustomId { get; }
    
    public ChildDomainWithCustomId(BranchDomain parent, Guid customId) : base(parent)
    {
        CustomId = customId;
    }
}

// ============================================================================
// Example 4: Time-Based Logic Desync
// ============================================================================

public class DesyncGameState4 : NetworkGameState
{
    public ObservableAttribute<int> Counter { get; }
    public ObservableAttribute<long> Timestamp { get; }
    
    public DesyncGameState4(Guid matchId) : base(matchId, matchId.GetHashCode())
    {
        Counter = new ObservableAttribute<int>(0);
        Timestamp = new ObservableAttribute<long>(0);
        Console.WriteLine($"      [DesyncGameState4] Created for match {matchId}");
        
        // Register reaction that observes counter changes
        new CounterReaction(this).AddTo(Disposables);
    }
}

public class CounterReaction : Reaction, IAfterReaction<DesyncGameState4, TimeBasedAction>
{
    public CounterReaction(DesyncGameState4 target) : base(target) { }
    
    public void OnAfter(DesyncGameState4 domain, TimeBasedAction action)
    {
        Console.WriteLine($"      [CounterReaction] Counter is now: {domain.Counter.Value}");
    }
}

public class DesyncGameState4Factory : IGameStateFactory<DesyncGameState4>
{
    public DesyncGameState4 CreateGameState(Guid matchId) => new DesyncGameState4(matchId);
}

public class TimeBasedAction : NetworkAction<DesyncGameState4, TimeBasedAction>
{
    protected override void ExecuteProcess(DesyncGameState4 domain)
    {
        Console.WriteLine("      [TimeBasedAction] Executing with time-based logic");
        
        // BAD: Using DateTime.Now - each instance gets different timestamp!
        var now = DateTime.Now.Ticks;
        domain.Timestamp.Value = now;
        
        // Different execution paths based on current time
        if (now % 2 == 0)
        {
            domain.Counter.Value++;
            Console.WriteLine($"      Even timestamp ({now}): Counter incremented to {domain.Counter.Value}");
        }
        else
        {
            domain.Counter.Value += 2;
            Console.WriteLine($"      Odd timestamp ({now}): Counter incremented by 2 to {domain.Counter.Value}");
        }
    }
}
