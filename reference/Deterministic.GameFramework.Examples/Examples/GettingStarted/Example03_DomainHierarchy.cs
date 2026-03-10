using Deterministic.GameFramework.Core;
using Deterministic.GameFramework.Core.Domain;

namespace Deterministic.GameFramework.Examples.GettingStarted;

/// <summary>
/// Example 3: Domain Hierarchy - Parent-child relationships, tree navigation
/// </summary>
public static class Example03_DomainHierarchy
{
    public static void Run()
    {
        var root = new RootDomain();
        
        // Build domain tree
        var player = new PlayerDomain(root, "Hero");
        new ItemDomain(player.Inventory, "Sword") { Value = 50 };
        new ItemDomain(player.Inventory, "Shield") { Value = 30 };
        
        // Navigate tree: find owner from item
        var firstItem = player.Inventory.Items.First();
        var owner = firstItem.GetInParent<PlayerDomain>();
        Console.WriteLine($"'{firstItem.Name}' belongs to: {owner?.Name}");
        
        // Query all items in tree
        var allItems = root.GetAll<ItemDomain>();
        Console.WriteLine($"Total items: {allItems.Count}");
        
        // Remove an item
        firstItem.Dispose();
        Console.WriteLine($"Items after removal: {player.Inventory.Items.Count()}");
        
        root.Dispose();
    }
}

// --- Domains ---

public class PlayerDomain : BranchDomain
{
    public string Name { get; set; }
    public InventoryDomain Inventory { get; }
    
    public PlayerDomain(BranchDomain parent, string name) : base(parent)
    {
        Name = name;
        Inventory = new InventoryDomain(this);
    }
}

public class InventoryDomain : BranchDomain
{
    public InventoryDomain(BranchDomain parent) : base(parent) { }
    
    public IEnumerable<ItemDomain> Items => GetAll<ItemDomain>();
}

public class ItemDomain : BranchDomain
{
    public string Name { get; set; }
    public int Value { get; set; }
    
    public ItemDomain(BranchDomain parent, string name) : base(parent)
    {
        Name = name;
    }
}
