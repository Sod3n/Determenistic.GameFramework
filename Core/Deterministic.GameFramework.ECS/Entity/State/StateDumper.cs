using System;
using System.Text;
using Deterministic.GameFramework.Utils.Logging;

namespace Deterministic.GameFramework.ECS;

public static class StateDumper
{
    public static string Dump(EntityWorld state)
    {
        var sb = new StringBuilder();
        sb.AppendLine("--- State Dump ---");

        DumpEntities(state, sb);

        return sb.ToString();
    }

    /// <summary>
    /// Logs a diagnostic diff on state mismatch.
    /// Distinguishes between logical state differences and memory/garbage (padding) issues.
    /// </summary>
    [System.Diagnostics.Conditional("DEBUG")]
    public static void LogMismatch(string label, long tick, Guid localHash, Guid serverHash, byte[]? snapshotData)
    {
        ILogger.LogError($"[{label}] STATE MISMATCH at Tick {tick}! Local: {localHash} != Server: {serverHash}");

        if (snapshotData == null)
        {
            ILogger.LogError($"[{label}] No snapshot data available for Tick {tick}");
            return;
        }

        // Round-trip test: deserialize → re-serialize → hash
        // If the round-trip hash matches the server, the original snapshot had garbage bytes (padding/alignment)
        // If it doesn't match, there's an actual logical state divergence
        try
        {
            var tempWorld = new EntityWorld();
            StateSerializer.Deserialize(tempWorld, snapshotData);
            byte[] reserialized = StateSerializer.Serialize(tempWorld);
            var roundTripHash = StateHasher.Hash(reserialized);

            if (roundTripHash == serverHash)
            {
                ILogger.LogWarning($"[{label}] Mismatch is MEMORY GARBAGE (padding/alignment). Round-trip hash matches server.");
                ILogger.LogWarning($"[{label}] Original bytes: {snapshotData.Length}, Re-serialized bytes: {reserialized.Length}");
                LogByteDiff(label, snapshotData, reserialized);
            }
            else
            {
                ILogger.LogError($"[{label}] Mismatch is LOGICAL STATE DIVERGENCE. Round-trip hash: {roundTripHash}");
                var sb = new StringBuilder();
                DumpEntities(tempWorld, sb);
                ILogger.LogError($"[{label}] State at Tick {tick}:\n{sb}");
            }
        }
        catch (Exception ex)
        {
            ILogger.LogError($"[{label}] Failed to analyze mismatch: {ex.Message}");
        }
    }

    private static void DumpEntities(EntityWorld state, StringBuilder sb)
    {
        for (int i = 0; i < state.EntityMasks.Length; i++)
        {
            if (state.EntityMasks[i].IsEmpty) continue;

            sb.AppendLine($"Entity {i}:");

            for (int typeId = 0; typeId < state._componentArrays.Length; typeId++)
            {
                if (state.EntityMasks[i].IsSet(typeId) && state._componentArrays[typeId] is { } array)
                {
                    var component = array.GetValue(i);
                    if (component != null)
                    {
                        sb.AppendLine($"  {component.GetType().Name}: {DumpComponent(component)}");
                    }
                }
            }
        }
    }

    private static string DumpComponent(object component)
    {
        var type = component.GetType();
        if (type.IsValueType)
        {
            var sb = new StringBuilder();
            sb.Append("{ ");
            foreach (var field in type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                sb.Append($"{field.Name}: {field.GetValue(component)}, ");
            }
            if (sb.Length > 2) sb.Length -= 2; // Remove trailing comma
            sb.Append(" }");
            return sb.ToString();
        }

        return component.ToString() ?? "null";
    }

    private static void LogByteDiff(string label, byte[] a, byte[] b)
    {
        int diffCount = 0;
        int maxLen = Math.Max(a.Length, b.Length);
        var sb = new StringBuilder();

        for (int i = 0; i < maxLen; i++)
        {
            byte ba = i < a.Length ? a[i] : (byte)0;
            byte bb = i < b.Length ? b[i] : (byte)0;
            if (ba != bb)
            {
                diffCount++;
                if (diffCount <= 32) // Limit output
                    sb.AppendLine($"  Offset 0x{i:X4}: 0x{ba:X2} vs 0x{bb:X2}");
            }
        }

        if (diffCount > 32)
            sb.AppendLine($"  ... and {diffCount - 32} more differing bytes");

        ILogger.LogWarning($"[{label}] {diffCount} byte(s) differ:\n{sb}");
    }
}
