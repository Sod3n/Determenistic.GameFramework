using Deterministic.GameFramework.Core;
using Deterministic.GameFramework.Core.Domain;

namespace Deterministic.GameFramework.Examples.GettingStarted;

/// <summary>
/// Example 1: Hello World - Domain and Action basics
/// </summary>
public static class Example01_HelloWorld
{
    public static void Run()
    {
        // Create root domain
        var root = new RootDomain();
        
        // Create a counter domain as child
        var counter = new CounterDomain(root);
        
        // Execute actions
        new IncrementAction { Amount = 1 }.Execute(counter);
        new IncrementAction { Amount = 5 }.Execute(counter);
        new IncrementAction { Amount = 10 }.Execute(counter);
        
        Console.WriteLine($"Final value: {counter.Value}"); // 16
        
        root.Dispose();
    }
}

// --- Domains ---

public class CounterDomain : LeafDomain
{
    public int Value { get; private set; } = 0;
    
    public CounterDomain(BranchDomain parent) : base(parent) { }
    
    public void Increment(int amount) => Value += amount;
}

// --- Actions ---

public class IncrementAction : DARAction<CounterDomain, IncrementAction>
{
    public int Amount { get; set; } = 1;
    
    protected override void ExecuteProcess(CounterDomain domain)
    {
        domain.Increment(Amount);
    }
}
