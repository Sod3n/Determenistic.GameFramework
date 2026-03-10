using System;
using System.Buffers.Binary;

namespace Deterministic.GameFramework.CoreV2;

public static class ComponentIdSerializer
{
    public static int GetMappingsSize()
    {
        // Format: Count (4) + [StableId (16) + DenseId (4)] * Count
        return 4 + (16 + 4) * ComponentId.StableToDense.Count;
    }

    public static int GetMappingsSize(System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<StableComponentId, DenseComponentId>> mappings)
    {
        return 4 + (16 + 4) * mappings.Count;
    }

    public static System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<StableComponentId, DenseComponentId>> GetMappingsSnapshot()
    {
        var sortedMappings = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<StableComponentId, DenseComponentId>>(ComponentId.StableToDense);
        sortedMappings.Sort((a, b) => a.Key.Value.CompareTo(b.Key.Value));
        return sortedMappings;
    }

    public static int WriteMappings(Span<byte> span, System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<StableComponentId, DenseComponentId>> mappings)
    {
        BinaryPrimitives.WriteInt32LittleEndian(span, mappings.Count);
        int offset = 4;
        
        foreach (var kvp in mappings)
        {
            byte[] guidBytes = kvp.Key.Value.ToByteArray();
            guidBytes.CopyTo(span.Slice(offset, 16));
            offset += 16;
            
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(offset), kvp.Value.Value);
            offset += 4;
        }
        return offset;
    }

    public static int WriteMappings(Span<byte> span)
    {
        // Backward compatibility / Unsafe version
        return WriteMappings(span, GetMappingsSnapshot());
    }

    public static byte[] SerializeMappings()
    {
        int size = GetMappingsSize();
        byte[] buffer = new byte[size];
        WriteMappings(buffer);
        return buffer;
    }
    
    public static void ImportMappings(byte[] data, bool apply = true)
    {
        var span = new ReadOnlySpan<byte>(data);
        ImportMappings(span, apply);
    }

    public static void ImportMappings(ReadOnlySpan<byte> span, bool apply = true)
    {
        if (span.Length < 4) return;

        int count = BinaryPrimitives.ReadInt32LittleEndian(span);
        int offset = 4;
        
        if (apply)
        {
            // Clear existing mappings to enforce authoritative server state
            ComponentId.ClearMappings();
        }

        for (int i = 0; i < count; i++)
        {
            if (offset + 20 > span.Length) break;

            byte[] guidBytes = span.Slice(offset, 16).ToArray();
            var stableGuid = new Guid(guidBytes);
            var stableId = new StableComponentId(stableGuid);
            offset += 16;
            
            int denseValue = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset));
            var denseId = new DenseComponentId(denseValue);
            offset += 4;
            
            if (apply)
            {
                ComponentId.RegisterMapping(stableId, denseId);
            }
        }
        
        if (apply)
        {
            Console.WriteLine($"[ComponentIdSerializer] Imported {count} mappings from Server.");
        }
    }
}
