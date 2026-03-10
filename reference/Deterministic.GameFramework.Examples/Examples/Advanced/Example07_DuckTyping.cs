using Newtonsoft.Json;
using Deterministic.GameFramework.Core;
using Deterministic.GameFramework.Core.Actions;
using Deterministic.GameFramework.Core.Domain;

namespace Deterministic.GameFramework.Examples.Advanced;

/// <summary>
/// Example 7: Duck Typing (Structural Typing)
/// - Actions work with any domain that has the required structure
/// - Demonstrates polymorphism without inheritance
/// - Shows how the same action can target different entity types
/// </summary>
public static class Example07_DuckTyping
{
    public static void Run()
    {
        var game = new GameWorld();
        
        // Create diverse entity types
        var player = new Player(game, "Hero") { Health = 100 };
        var enemy = new Enemy(game, "Goblin") { Health = 50 };
        var wall = new Wall(game) { Health = 200 };
        var bullet = new Bullet(game) { Health = 1 };
        
        Console.WriteLine("=== Initial State ===");
        PrintHealth(player, enemy, wall, bullet);
        
        // Same action works on ALL of them
        var damage = new DamageAction { Amount = 25 };
        
        Console.WriteLine("\n=== Applying 25 damage to each entity ===");
        damage.Execute(player);
        damage.Execute(enemy);
        damage.Execute(wall);
        damage.Execute(bullet);
        
        PrintHealth(player, enemy, wall, bullet);
        
        // Demonstrate action forwarding
        Console.WriteLine("\n=== Domain Forwarding ===");
        var playerWithArmor = new Player(game, "Knight") { Health = 100 };
        var armor = new ArmorComponent(playerWithArmor) { Defense = 10 };
        
        Console.WriteLine($"Knight has armor component (Defense: {armor.Defense})");
        Console.WriteLine($"Before: Knight HP = {playerWithArmor.Health}");
        
        // Execute on armor - automatically forwards to player
        damage.Execute(armor);
        
        Console.WriteLine($"After: Knight HP = {playerWithArmor.Health}");
        Console.WriteLine("Action automatically found the IDamageable parent!");
        
        game.Dispose();
    }
    
    private static void PrintHealth(params IDamageable[] entities)
    {
        foreach (var entity in entities)
        {
            var name = entity switch
            {
                Player p => $"Player '{p.Name}'",
                Enemy e => $"Enemy '{e.Type}'",
                Wall => "Wall",
                Bullet => "Bullet",
                _ => "Unknown"
            };
            Console.WriteLine($"{name,-20} HP: {entity.Health}");
        }
    }
}

// --- Interfaces (Define Structure) ---

public interface IDamageable : IDomain
{
    int Health { get; set; }
}

public interface IMovable : IDomain
{
    float X { get; set; }
    float Y { get; set; }
}

// --- Game World ---

public class GameWorld : BranchDomain
{
    private readonly Dictionary<int, LeafDomain> _registry = new();
    private int _nextId = 1;
    
    public GameWorld() : base(null)
    {
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

// --- Diverse Entity Types ---

// Player: BranchDomain + IDamageable + IMovable
public class Player : BranchDomain, IDamageable, IMovable
{
    public string Name { get; }
    public int Health { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    
    public Player(BranchDomain parent, string name) : base(parent)
    {
        Name = name;
    }
}

// Enemy: BranchDomain + IDamageable + IMovable
public class Enemy : BranchDomain, IDamageable, IMovable
{
    public string Type { get; }
    public int Health { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    
    public Enemy(BranchDomain parent, string type) : base(parent)
    {
        Type = type;
    }
}

// Wall: LeafDomain + IDamageable (not movable)
public class Wall : LeafDomain, IDamageable
{
    public int Health { get; set; }
    
    public Wall(BranchDomain parent) : base(parent)
    {
    }
}

// Bullet: LeafDomain + IDamageable + IMovable
public class Bullet : LeafDomain, IDamageable, IMovable
{
    public int Health { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    
    public Bullet(BranchDomain parent) : base(parent)
    {
    }
}

// ArmorComponent: LeafDomain (not damageable itself)
public class ArmorComponent : LeafDomain
{
    public int Defense { get; set; }
    
    public ArmorComponent(BranchDomain parent) : base(parent)
    {
    }
}

// --- Actions ---

// Universal damage action - works with ANY IDamageable
public class DamageAction : DARAction<IDamageable, DamageAction>
{
    [JsonProperty] public int Amount { get; set; }
    
    protected override void ExecuteProcess(IDamageable target)
    {
        target.Health -= Amount;
        
        var name = target switch
        {
            Player p => $"Player '{p.Name}'",
            Enemy e => $"Enemy '{e.Type}'",
            Wall => "Wall",
            Bullet => "Bullet",
            _ => "Entity"
        };
        
        Console.WriteLine($"  → {name} took {Amount} damage");
    }
}

// Movement action - works with ANY IMovable
public class MoveAction : DARAction<IMovable, MoveAction>
{
    [JsonProperty] public float DeltaX { get; set; }
    [JsonProperty] public float DeltaY { get; set; }
    
    protected override void ExecuteProcess(IMovable target)
    {
        target.X += DeltaX;
        target.Y += DeltaY;
    }
}
