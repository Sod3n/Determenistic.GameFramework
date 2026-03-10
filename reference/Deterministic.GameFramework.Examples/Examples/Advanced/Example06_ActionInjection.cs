using Newtonsoft.Json;
using Deterministic.GameFramework.Core;
using Deterministic.GameFramework.Core.Actions;
using Deterministic.GameFramework.Core.Domain;

namespace Deterministic.GameFramework.Examples.Advanced;

/// <summary>
/// Example 6: Action Injection &amp; Domain References
/// - Inject dependencies into actions via interfaces
/// - Reference domains by ID for network serialization
/// </summary>
public static class Example06_ActionInjection
{
    public static void Run()
    {
        var game = new SimpleGameState();
        var knight = new Unit(game, "Knight") { Attack = 20 };
        var goblin = new Unit(game, "Goblin") { Health = 50 };
        
        Console.WriteLine($"Knight ID: {knight.Id}, Goblin ID: {goblin.Id}");
        Console.WriteLine($"Before: Goblin HP = {goblin.Health}");
        
        // Create action with just the target ID (network-safe)
        var action = new AttackAction { TargetId = goblin.Id };
        
        // Simulate network: serialize → deserialize
        var json = JsonConvert.SerializeObject(action);
        Console.WriteLine($"Serialized: {json}");
        
        var received = JsonConvert.DeserializeObject<AttackAction>(json)!;
        
        // Execute - Game is injected, target resolved by ID
        received.Execute(knight);
        
        Console.WriteLine($"After: Goblin HP = {goblin.Health}");
        
        game.Dispose();
    }
}

// --- Injection Interface ---

public interface IRequireGame : IDARAction
{
    SimpleGameState Game { get; set; }
}

// --- Domains ---

public class SimpleGameState : BranchDomain
{
    private readonly Dictionary<int, LeafDomain> _registry = new();
    private int _nextId = 1;
    
    public SimpleGameState() : base(null)
    {
        // Inject this GameState into any action that needs it
        new Reaction<LeafDomain, IRequireGame>(this)
            .Prepare((_, action) => action.Game = this)
            .AddTo(Disposables);
        
        // Assign IDs and track domains
        new Reaction<LeafDomain, AddSubdomainAction>(this)
            .After((_, action) =>
            {
                action.Child.Id = _nextId++;
                _registry[action.Child.Id] = action.Child;
            })
            .AddTo(Disposables);
    }
    
    public LeafDomain? GetDomain(int id) => 
        _registry.TryGetValue(id, out var d) ? d : null;
}

public class Unit : BranchDomain
{
    public string Name { get; }
    public int Health { get; set; } = 100;
    public int Attack { get; set; } = 10;
    
    public Unit(BranchDomain parent, string name) : base(parent)
    {
        Name = name;
    }
}

// --- Actions ---

public class AttackAction : DARAction<Unit, AttackAction>, IRequireGame
{
    [JsonIgnore] public SimpleGameState Game { get; set; }  // Injected (not serialized)
    
    [JsonProperty] public int TargetId { get; set; }  // Serialized
    
    protected override void ExecuteProcess(Unit attacker)
    {
        // Use injected Game to resolve target by ID
        var target = Game.GetDomain(TargetId) as Unit;
        if (target == null) return;
        
        target.Health -= attacker.Attack;
        Console.WriteLine($"{attacker.Name} attacks {target.Name} for {attacker.Attack} damage");
    }
}
