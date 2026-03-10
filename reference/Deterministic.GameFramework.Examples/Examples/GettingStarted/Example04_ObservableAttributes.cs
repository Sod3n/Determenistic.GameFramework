using Deterministic.GameFramework.Core;
using Deterministic.GameFramework.Core.Domain;

namespace Deterministic.GameFramework.Examples.GettingStarted;

/// <summary>
/// Example 4: Observable Attributes - Reactive UI Bindings (MVVM/MVC Pattern)
/// - Demonstrates separation between Model (domain) and View (UI)
/// - Shows automatic UI updates when model state changes
/// - Illustrates proper use of ObservableAttribute for reactive patterns
/// </summary>
public static class Example04_ObservableAttributes
{
    public static void Run()
    {
        var game = new Game04Domain();
        
        // Model: Create player domain with observable health
        var player = new Player04Domain(game, "Hero");
        
        // View: Create UI that observes the model
        var healthBar = new HealthBarUI(player);
        
        Console.WriteLine("=== Reactive UI Demo ===");
        Console.WriteLine();
        
        // Model changes automatically update the UI
        Console.WriteLine("Taking 30 damage...");
        player.Health.Value -= 30;
        
        Console.WriteLine("\nTaking 50 more damage...");
        player.Health.Value -= 50;
        
        Console.WriteLine("\nHealing 40 HP...");
        player.Health.Value += 40;
        
        game.Dispose();
    }
}

// --- Model (Domain Layer) ---

public class Game04Domain : BranchDomain
{
    public Game04Domain() : base(null) { }
}

public class Player04Domain : BranchDomain
{
    public string Name { get; }
    
    // Observable attribute - UI can bind to this
    public ObservableAttribute<int> Health { get; } = new(100);
    
    public Player04Domain(BranchDomain parent, string name) : base(parent)
    {
        Name = name;
    }
}

// --- View (UI Layer) ---

// UI component that observes player health
public class HealthBarUI : LeafDomain
{
    public HealthBarUI(Player04Domain player) : base(player)
    {
        // Bind to player's health - automatically updates when health changes
        player.Health.Observe(this, health =>
        {
            var barLength = 20;
            var filled = (int)((health / 100.0) * barLength);
            var bar = new string('█', filled) + new string('░', barLength - filled);
            
            var color = health switch
            {
                <= 20 => "RED",
                <= 50 => "YELLOW",
                _ => "GREEN"
            };
            
            Console.WriteLine($"  [UI] [{bar}] {health}/100 HP ({color})");
            
            if (health <= 20)
                Console.WriteLine($"       ⚠️  Low health warning!");
        });
    }
}
