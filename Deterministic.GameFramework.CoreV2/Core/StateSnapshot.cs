using System;
using System.Collections.Generic;
using MessagePack;

namespace Deterministic.GameFramework.CoreV2;

[MessagePackObject]
public class StateSnapshot
{
    [Key(0)]
    public int NextEntityId { get; set; }

    [Key(1)]
    public int EntityCapacity { get; set; }

    [Key(2)]
    public Dictionary<string, byte[]> ExternalState { get; set; } = new();

    [Key(3)]
    public ReadOnlyMemory<byte> EntityMasks { get; set; }

    [Key(4)]
    public List<ComponentSnapshot> Components { get; set; } = new();

    [Key(5)]
    public List<MappingSnapshot> Mappings { get; set; } = new();

    public void Clear()
    {
        ExternalState.Clear();
        Components.Clear();
        Mappings.Clear();
        EntityMasks = ReadOnlyMemory<byte>.Empty;
    }
}

[MessagePackObject]
public struct ComponentSnapshot
{
    [Key(0)]
    public int TypeId { get; set; }

    [Key(1)]
    public ReadOnlyMemory<byte> Data { get; set; }

    [Key(2)]
    public int Count { get; set; }
}

[MessagePackObject]
public struct MappingSnapshot
{
    [Key(0)]
    public ReadOnlyMemory<byte> StableId { get; set; }

    [Key(1)]
    public int DenseId { get; set; }
}
