using Deterministic.GameFramework.Core;
using Deterministic.GameFramework.Core.Data;
using Deterministic.GameFramework.Core.Domain;
using Newtonsoft.Json;

namespace Deterministic.GameFramework.Examples.Advanced;

/// <summary>
/// Example: Data-Driven Component Architecture
/// Shows how to build game objects from JSON using composable components.
/// Pattern: JSON → Components → Item Domain
/// </summary>
public static class Example_DataDrivenComponents
{
    public static void Run()
    {
        Console.WriteLine("=== Data-Driven Components ===\n");
        
        // Load data from JSON (automatically finds Data folder next to this file)
        GameData.LoadAsync().Wait();
        
        // Create items from data - each item is composed of components
        var root = new RootDomain();
        var sword = ItemFactory.Create(root, "sword");
        var potion = ItemFactory.Create(root, "potion");
        
        // Items are built from components
        Console.WriteLine($"{sword.Name}:");
        Console.WriteLine($"  - Components: {string.Join(", ", sword.GetComponentNames())}");
        
        Console.WriteLine($"\n{potion.Name}:");
        Console.WriteLine($"  - Components: {string.Join(", ", potion.GetComponentNames())}");
        
        root.Dispose();
    }
}

// Step 1: Define data structure (components in JSON)
public class ComponentEntry
{
    [JsonProperty("type")] public string Type { get; set; } = "";
    [JsonProperty("value")] public int? Value { get; set; }
}

public class ItemEntry
{
    [JsonProperty("name")] public string Name { get; set; } = "";
    [JsonProperty("components")] public List<ComponentEntry> Components { get; set; } = new();
}

// Step 2: Create data loader
public class ItemsData : GameData<ItemEntry>
{
    public override string Path => "Items.json";
    private Dictionary<string, ItemEntry> _items = new();
    
    public override void Load(Dictionary<string, ItemEntry> entries)
    {
        _items = entries;
    }
    
    public ItemEntry? Get(string id) => _items.GetValueOrDefault(id);
}

// Step 3: Define components as child domains
public class DamageComponent : LeafDomain
{
    public int Value { get; set; }
    public DamageComponent(BranchDomain parent, int value) : base(parent) { Value = value; }
}

public class HealComponent : LeafDomain
{
    public int Value { get; set; }
    public HealComponent(BranchDomain parent, int value) : base(parent) { Value = value; }
}

// Step 4: Item is a BranchDomain that contains components
public class Item : BranchDomain
{
    public string Name { get; set; } = "";
    
    public Item(BranchDomain parent) : base(parent) { }
    
    public IEnumerable<string> GetComponentNames()
    {
        foreach (var child in GetChildren())
        {
            yield return child.GetType().Name.Replace("Component", "");
        }
    }
}

// Step 5: Factory creates item + components from data
public static class ItemFactory
{
    public static ItemsData Items = new();
    
    public static Item Create(BranchDomain parent, string itemId)
    {
        var data = Items.Get(itemId)!;
        var item = new Item(parent) { Name = data.Name };
        
        // Create component domains from data
        foreach (var component in data.Components)
        {
            switch (component.Type)
            {
                case "damage":
                    new DamageComponent(item, component.Value ?? 0);
                    break;
                case "heal":
                    new HealComponent(item, component.Value ?? 0);
                    break;
            }
        }
        
        return item;
    }
}

// Step 6: Load data at startup
public static class GameData
{
    public static async Task LoadAsync([System.Runtime.CompilerServices.CallerFilePath] string? callerPath = null)
    {
        await GameDataLoader.LoadAsync(ItemFactory.Items, callerPath: callerPath);
    }
}
