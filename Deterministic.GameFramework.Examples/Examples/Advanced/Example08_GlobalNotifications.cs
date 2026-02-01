using Deterministic.GameFramework.Core;
using Deterministic.GameFramework.Core.Domain;

namespace Deterministic.GameFramework.Examples.Advanced;

/// <summary>
/// Example 8: Global Notifications via Root Domain Reactions
/// 
/// Pattern: Cross-domain communication through root domain subscriptions.
/// Domain B subscribes to Domain A's actions by attaching reactions to the root.
/// When Domain A executes an action on itself, Domain B's reactions are triggered.
/// 
/// Architecture:
///   Root
///   ├── Domain A (publishes actions)
///   └── Domain B (subscribes to A's actions via root)
/// 
/// Use cases:
/// - Cross-system coordination (battle system → UI system)
/// - Achievement tracking (player actions → achievement system)
/// - Analytics (game events → telemetry system)
/// - Network broadcasting (game state → network layer)
/// - Decoupled feature systems
/// </summary>
public static class Example08_GlobalNotifications
{
    public static void Run()
    {
        var root = new RootDomain();
        
        // Domain A: publishes events
        var domainA = new DomainA(root);
        
        // Domain B: subscribes to Domain A's events via root
        var domainB = new DomainB(root);
        
        // Domain A executes action on itself
        // Domain B's reaction triggers automatically
        domainA.DoSomething("Hello from A!");
        
        root.Dispose();
    }
}

// --- Domain A (Publisher) ---
// Executes actions on itself, unaware of subscribers

public class DomainA : BranchDomain
{
    public DomainA(BranchDomain parent) : base(parent) { }
    
    public void DoSomething(string message)
    {
        Console.WriteLine($"[Domain A] Doing something: {message}");
        
        // Execute action on self - reactions on root will trigger
        new SomethingHappenedAction { Message = message }.Execute(this);
    }
}

// --- Domain B (Subscriber) ---
// Subscribes to Domain A's actions via root

public class DomainB : BranchDomain
{
    public DomainB(BranchDomain parent) : base(parent)
    {
        // Get reference to root
        var root = GetInParent<RootDomain>(includeSelf: false);
        
        // Subscribe to Domain A's action via root
        new Reaction<LeafDomain, SomethingHappenedAction>(root)
            .After((_, action) => 
            {
                Console.WriteLine($"[Domain B] Received notification: {action.Message}");
            })
            .AddTo(Disposables);
    }
}


// --- Action (Event Message) ---

public class SomethingHappenedAction : DARAction<LeafDomain, SomethingHappenedAction>
{
    public string Message { get; set; }
    
    protected override void ExecuteProcess(LeafDomain domain)
    {
        // Empty - this is a notification action
    }
}

// --- Key Takeaways ---
//
// Pattern Flow:
// 1. Domain B gets root: GetInParent<RootDomain>()
// 2. Domain B subscribes: new Reaction<LeafDomain, SomethingHappenedAction>(root).After(...)
// 3. Domain A executes: new SomethingHappenedAction().Execute(this)
// 4. Action propagates up to root → Domain B's reaction triggers
//
// Benefits:
// - Decoupled: Domain A doesn't know about Domain B
// - Multiple domains can subscribe to the same action
// - Type-safe and network-safe
// - No direct dependencies between siblings
