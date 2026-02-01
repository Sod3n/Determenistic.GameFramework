using System;
using System.Collections.Generic;
using System.Text;
using Deterministic.GameFramework.Core;
using Deterministic.GameFramework.Core.Utils;
using Deterministic.GameFramework.Server;

namespace Deterministic.GameFramework.Examples.Network;

/// <summary>
/// Example demonstrating serialization optimization strategies.
/// Shows size differences between Optimized and Informative modes.
/// </summary>
public class Example08_SerializationComparison
{
    public static void Run()
    {
        Console.WriteLine("=== Serialization Optimization Example ===\n");
        
        var actions = CreateSampleActions();
        Console.WriteLine($"Testing with {actions.Count} sample actions\n");
        
        // Measure both modes
        var informativeJson = JsonSerializer.ToJson(actions, SerializationMode.Informative);
        var informativeSize = Encoding.UTF8.GetByteCount(informativeJson);
        
        var optimizedJson = JsonSerializer.ToJson(actions, SerializationMode.Optimized);
        var optimizedSize = Encoding.UTF8.GetByteCount(optimizedJson);
        
        // Show size comparison
        Console.WriteLine("Size Comparison:");
        Console.WriteLine($"  Informative: {informativeSize,5} bytes (readable, full type info)");
        Console.WriteLine($"  Optimized:   {optimizedSize,5} bytes (compact, minimal types)");
        Console.WriteLine($"  Reduction:   {100.0 * (informativeSize - optimizedSize) / informativeSize:F1}%");
        Console.WriteLine();
        
        // MessagePack note
        Console.WriteLine("MessagePack:");
        Console.WriteLine("  Requires Union attributes on all action types (maintenance overhead)");
        Console.WriteLine("  Typical savings: Additional 20-30% vs optimized JSON");
        Console.WriteLine("  Recommendation: Stick with optimized JSON unless bandwidth is critical");
        Console.WriteLine();
        
        // Bandwidth impact (20Hz sync rate)
        var syncRate = 20;
        Console.WriteLine($"Bandwidth Impact (at {syncRate}Hz sync rate):");
        Console.WriteLine($"  Per player:  {optimizedSize * syncRate / 1024.0:F1} KB/s (optimized)");
        Console.WriteLine($"  100 players: {optimizedSize * syncRate * 100 / 1024.0 / 1024.0:F2} MB/s");
        Console.WriteLine();
        
        // Quick recommendations
        Console.WriteLine("Usage:");
        Console.WriteLine("  Development:  JsonSerializer.SetMode(SerializationMode.Informative)");
        Console.WriteLine("  Production:   JsonSerializer.SetMode(SerializationMode.Optimized) [default]");
        Console.WriteLine("  High-scale:   Use MessagePack for binary serialization");
    }
    
    private static List<INetworkAction> CreateSampleActions()
    {
        var matchId = Guid.NewGuid();
        
        return new List<INetworkAction>
        {
            new MoveAction { MatchId = matchId, DomainId = 1, X = 10, Y = 20, Speed = 5.5f },
            new AttackAction { MatchId = matchId, DomainId = 2, TargetId = Guid.NewGuid(), Damage = 25 },
            new MoveAction { MatchId = matchId, DomainId = 1, X = 15, Y = 25, Speed = 3.2f },
            new UseItemAction { MatchId = matchId, DomainId = 3, ItemId = "potion_health", Quantity = 1 },
            new AttackAction { MatchId = matchId, DomainId = 2, TargetId = Guid.NewGuid(), Damage = 30 },
        };
    }
}

// Sample action classes for demonstration
public class MoveAction : NetworkAction<LeafDomain, MoveAction>
{
    public int X { get; set; }
    public int Y { get; set; }
    public float Speed { get; set; }
    
    protected override void ExecuteProcess(LeafDomain game)
    {
        // Implementation not needed for serialization example
    }
}

public class AttackAction : NetworkAction<LeafDomain, AttackAction>
{
    public Guid TargetId { get; set; }
    public int Damage { get; set; }
    
    protected override void ExecuteProcess(LeafDomain game)
    {
        // Implementation not needed for serialization example
    }
}

public class UseItemAction : NetworkAction<LeafDomain, UseItemAction>
{
    public string ItemId { get; set; } = "";
    public int Quantity { get; set; }
    
    protected override void ExecuteProcess(LeafDomain game)
    {
        // Implementation not needed for serialization example
    }
}
