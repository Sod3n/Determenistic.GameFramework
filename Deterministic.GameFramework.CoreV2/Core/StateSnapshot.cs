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
    public List<ComponentSnapshot> Components { get; set; } = new();

    [Key(4)]
    public List<MappingSnapshot> Mappings { get; set; } = new();
}

[MessagePackObject]
public struct ComponentSnapshot
{
    [Key(0)]
    public int TypeId { get; set; }

    [Key(1)]
    public byte[] Data { get; set; }

    [Key(2)]
    public byte[] PresenceMask { get; set; }

    [Key(3)]
    public int Count { get; set; }
}

[MessagePackObject]
public struct MappingSnapshot
{
    [Key(0)]
    public byte[] StableId { get; set; }

    [Key(1)]
    public int DenseId { get; set; }
}
