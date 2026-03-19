using System.Reflection;
using Deterministic.GameFramework.CoreV2;
using Deterministic.GameFramework.CoreV2.Example.Components;

namespace Deterministic.GameFramework.CoreV2.Example;

public static class DebugTests
{
    public static void Run()
    {
        Console.WriteLine("\n--- Starting Debug Tests ---");
        TestBitMask();
        TestGlobalState();
        TestComponentIDs();
    }

    private static void TestBitMask()
    {
        Console.WriteLine("Test 1: BitMask128");
        var mask = new BitMask128();
        
        mask.Set(1);
        if (!mask.IsSet(1)) Console.WriteLine("FAILURE: Set(1) failed");
        
        mask.Set(107);
        if (!mask.IsSet(107)) Console.WriteLine("FAILURE: Set(107) failed");
        else Console.WriteLine("SUCCESS: Set(107) works");
    }

    private static void TestComponentIDs()
    {
        Console.WriteLine("Test 2: Component IDs");
        var attr = typeof(RegionDamageReactionTag).GetCustomAttribute<StableIdAttribute>();
        Guid tagId = attr?.Id ?? Guid.Empty;
        Console.WriteLine($"RegionDamageReactionTag ID: {tagId}");
        
        if (tagId != Guid.Parse("00000000-0000-0000-0000-000000000107")) Console.WriteLine($"WARNING: Expected ID 00000000-0000-0000-0000-000000000107, got {tagId}");
    }

    private static void TestGlobalState()
    {
        Console.WriteLine("Test 3: GlobalState Component Tracking");
        var state = new GlobalState();
        var entity = state.CreateEntity(); // ID 0
        
        Console.WriteLine($"Entity ID: {entity.Id}");
        
        // Add component
        state.AddComponent(entity, new RegionDamageReactionTag());
        
        // Check HasComponent
        bool hasComp = state.HasComponent<RegionDamageReactionTag>(entity);
        Console.WriteLine($"HasComponent<RegionDamageReactionTag>: {hasComp}");
        
        // Check Mask directly via Reflection or if we can expose it?
        // We can't access _entityMasks directly as it is internal.
        // But HasComponent uses it.
        
        if (hasComp) Console.WriteLine("SUCCESS: GlobalState tracks component correctly.");
        else Console.WriteLine("FAILURE: GlobalState failed to track component.");
        
        // Check if ID is correct
        var attr = typeof(RegionDamageReactionTag).GetCustomAttribute<StableIdAttribute>();
        Guid typeId = attr?.Id ?? Guid.Empty;
        // Re-add to trigger logging if enabled
        state.AddComponent(entity, new RegionDamageReactionTag());
    }
}
