# Serialization Optimization

This guide covers strategies for optimizing network bandwidth in your multiplayer game.

## Overview

The framework provides flexible serialization options to balance between **bandwidth efficiency** and **debugging convenience**. You're already using best practices:

- ✅ **Input-only sync** - Sending actions (inputs) not full game state
- ✅ **Batching** - NetworkSyncManager batches actions at 20Hz
- ✅ **Deterministic simulation** - Clients replay inputs locally

This guide shows how to further optimize serialization for production use.

## Serialization Modes

The framework provides two built-in serialization modes:

### 1. Optimized Mode (Default)

Minimizes bandwidth for production use:

```csharp
using Deterministic.GameFramework.Core.Utils;

// Set optimized mode (default)
JsonSerializer.SetMode(SerializationMode.Optimized);

var json = JsonSerializer.ToJson(actions);
// Output: {"actions":[{"type":"Move","x":5,"y":10}]}
// - Compact (no whitespace)
// - Minimal type info
// - Ignores nulls/defaults
```

**Characteristics:**
- No formatting (compact JSON)
- `TypeNameHandling.Auto` - Only includes `$type` when needed
- `PreserveReferencesHandling.None` - No `$id`/`$ref` overhead
- Ignores null values and defaults
- **~50% smaller** than informative mode

### 2. Informative Mode

Maximizes readability for debugging:

```csharp
// Set informative mode for debugging
JsonSerializer.SetMode(SerializationMode.Informative);

var json = JsonSerializer.ToJson(actions);
// Output (formatted):
// {
//   "$type": "System.Collections.Generic.List`1[[NetworkAction]]",
//   "$values": [
//     {
//       "$type": "MoveAction",
//       "X": 5,
//       "Y": 10,
//       "Timestamp": null
//     }
//   ]
// }
```

**Characteristics:**
- Indented formatting (human-readable)
- `TypeNameHandling.All` - Full type information
- `PreserveReferencesHandling.Objects` - Tracks references
- Includes null values for clarity
- Best for logging and debugging

### Per-Call Override

You can override the mode for specific serialization calls:

```csharp
// Use optimized for network
var networkJson = JsonSerializer.ToJson(actions, SerializationMode.Optimized);

// Use informative for logging
var logJson = JsonSerializer.ToJson(actions, SerializationMode.Informative);
Console.WriteLine(logJson);
```

## MessagePack (Optional)

For maximum bandwidth efficiency, use MessagePack binary serialization:

### What is MessagePack?

MessagePack is a binary serialization format that provides:
- **40-60% smaller** payloads than optimized JSON
- **2-5x faster** serialization/deserialization
- Type-safe binary encoding
- No human-readable overhead

### Setup

1. Install MessagePack NuGet packages:

```bash
# Server
dotnet add package MessagePack
dotnet add package Microsoft.AspNetCore.SignalR.Protocols.MessagePack

# Client (if using .NET)
dotnet add package MessagePack
dotnet add package Microsoft.AspNetCore.SignalR.Client.MessagePackProtocol
```

2. Configure SignalR to use MessagePack:

**Server:**
```csharp
builder.Services.AddSignalR()
    .AddMessagePackProtocol(); // Use MessagePack instead of JSON
```

**Client:**
```csharp
var connection = new HubConnectionBuilder()
    .WithUrl("http://localhost:5000/gamehub")
    .AddMessagePackProtocol() // Use MessagePack
    .Build();
```

### Using MessagePackSerializer

The framework provides an optional `MessagePackSerializer` utility:

```csharp
using Deterministic.GameFramework.Core.Utils;

// Serialize to binary
byte[] data = MessagePackSerializer.ToBytes(actions);

// Deserialize from binary
var actions = MessagePackSerializer.FromBytes<List<INetworkAction>>(data);

// Size comparison
var jsonSize = JsonSerializer.ToJson(actions).Length;
var msgpackSize = data.Length;
Console.WriteLine($"JSON: {jsonSize} bytes, MessagePack: {msgpackSize} bytes");
// Typical output: JSON: 450 bytes, MessagePack: 180 bytes (60% reduction)
```

## Bandwidth Comparison

Here's the impact of different optimization strategies:

| Strategy | Payload Size | Notes |
|----------|--------------|-------|
| JSON Informative | 100% (baseline) | Human-readable, full type info |
| JSON Optimized | ~50-70% | Compact JSON, minimal types |
| MessagePack | ~30-40% | Binary format, not readable |
| MessagePack + Compression* | ~10-15% | With HTTP compression |

*HTTP compression (gzip/brotli) is typically handled by your hosting infrastructure.

## Choosing a Strategy

### Development Phase
```csharp
// Use informative mode for easy debugging
JsonSerializer.SetMode(SerializationMode.Informative);
```

**Benefits:**
- Easy to inspect in browser DevTools
- Clear error messages
- Readable logs

### Production Phase

**Option A: Optimized JSON** (Recommended for most cases)
```csharp
JsonSerializer.SetMode(SerializationMode.Optimized);
```

**Benefits:**
- Good bandwidth savings (50%)
- Still debuggable if needed
- No additional dependencies
- Works with all SignalR clients

**Option B: MessagePack** (For high-scale production)
```csharp
builder.Services.AddSignalR()
    .AddMessagePackProtocol();
```

**Benefits:**
- Maximum bandwidth savings (60-70%)
- Faster serialization
- Best for mobile/limited bandwidth

**Trade-offs:**
- Not human-readable
- Requires MessagePack on all clients
- Harder to debug network issues

## Hybrid Approach

Use both strategies based on context:

```csharp
// In NetworkSyncManager or GameHub
public void BroadcastActions(Guid matchId, List<INetworkAction> actions)
{
    // Use optimized JSON for network
    var json = JsonSerializer.ToJson(actions, SerializationMode.Optimized);
    
    // Log with informative mode for debugging
    if (debugMode)
    {
        var debugJson = JsonSerializer.ToJson(actions, SerializationMode.Informative);
        _logger.LogDebug("Broadcasting actions: {Actions}", debugJson);
    }
    
    // Send to clients
    Clients.Group(matchId.ToString()).SendAsync("SyncActions", json);
}
```

## Security Considerations

The framework includes security measures by default:

### Dangerous Type Blocking

Both serializers block dangerous types:
```csharp
// These types are blocked by default
// - System.IO.File
// - System.Diagnostics.Process
// - System.Reflection.Assembly
```

### Optional Whitelist Mode

For maximum security, enable whitelist mode:

```csharp
// Only allow specific namespaces
var allowed = JsonSerializer.SafeSerializationBinder.GetRecommendedAllowedNamespaces();
allowed.Add("MyGame.Actions");
allowed.Add("MyGame.Shared");

JsonSerializer.EnableWhitelist(allowed);
```

See the [Security](../advanced/08-serialization-security.md) guide for more details.

## Best Practices

1. **Start with Optimized JSON** - Good balance of efficiency and debuggability
2. **Use Informative mode for logging** - Override mode for debug output
3. **Consider MessagePack for production** - If bandwidth is critical
4. **Monitor payload sizes** - Log sizes during development
5. **Test with real data** - Measure actual bandwidth savings

## Example: Measuring Bandwidth

```csharp
public void MeasureSerializationSize(List<INetworkAction> actions)
{
    // JSON Informative
    var informativeJson = JsonSerializer.ToJson(actions, SerializationMode.Informative);
    var informativeSize = System.Text.Encoding.UTF8.GetByteCount(informativeJson);
    
    // JSON Optimized
    var optimizedJson = JsonSerializer.ToJson(actions, SerializationMode.Optimized);
    var optimizedSize = System.Text.Encoding.UTF8.GetByteCount(optimizedJson);
    
    // MessagePack (if available)
    var msgpackData = MessagePackSerializer.ToBytes(actions);
    var msgpackSize = msgpackData.Length;
    
    Console.WriteLine($"Informative JSON: {informativeSize} bytes");
    Console.WriteLine($"Optimized JSON: {optimizedSize} bytes ({100 * optimizedSize / informativeSize}%)");
    Console.WriteLine($"MessagePack: {msgpackSize} bytes ({100 * msgpackSize / informativeSize}%)");
}
```

## Next Steps

- See [Example: Serialization Comparison](../../Examples/Network/SerializationComparison) for a working demo
- Learn about [Security Configuration](../advanced/08-serialization-security.md)
- Explore [Network Performance Tuning](05-network-threads.md)
