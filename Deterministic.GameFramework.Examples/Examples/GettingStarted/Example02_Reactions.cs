using Deterministic.GameFramework.Core;
using Deterministic.GameFramework.Core.Actions;
using Deterministic.GameFramework.Core.Domain;

namespace Deterministic.GameFramework.Examples.GettingStarted;

/// <summary>
/// Example 2: Reactions - Intercept actions automatically
/// </summary>
public static class Example02_Reactions
{
    public static void Run()
    {
        // Create root with an ID provider
        var root = new RootDomain();
        var idProvider = new SimpleIdProvider(root);
        
        // Add domains - they automatically get IDs assigned!
        var alice = new ActorDomain(root, "Alice");
        var bob = new ActorDomain(root, "Bob");
        
        Console.WriteLine($"'{alice.Name}' got ID: {alice.Id}");
        Console.WriteLine($"'{bob.Name}' got ID: {bob.Id}");
        Console.WriteLine($"Total IDs assigned: {idProvider.CurrentCounter - 1}");
        
        root.Dispose();
    }
}

// --- Domains ---

public class ActorDomain : BranchDomain
{
    public string Name { get; }
    
    public ActorDomain(BranchDomain parent, string name) : base(parent)
    {
        Name = name;
    }
}

/// <summary>
/// Assigns sequential IDs to new domains via reaction.
/// </summary>
public class SimpleIdProvider : BranchDomain
{
    private int _counter = 1;
    
    public int CurrentCounter => _counter;
    
    public SimpleIdProvider(BranchDomain target) : base(target)
    {
        // React to ANY domain being added anywhere in the tree
        new Reaction<LeafDomain, AddSubdomainAction>(target)
            .After((_, action) => action.Child.Id = _counter++)
            .AddTo(Disposables);
    }
}
