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

            for (int typeId = 0; typeId < state._componentStores.Length; typeId++)
            {
                if (state.EntityMasks[i].IsSet(typeId) && state._componentStores[typeId] is { } store)
                {
                    var component = store.GetValue(i);
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
            // Use raw byte dump instead of field.GetValue() to avoid
            // SIGBUS (EXC_ARM_DA_ALIGN) on Apple Silicon when Pack=1
            // structs have misaligned long fields — FieldDesc::GetInstanceField
            // uses aligned reads that crash on ARM.
            int size = System.Runtime.InteropServices.Marshal.SizeOf(type);
            var bytes = new byte[size];
            var handle = System.Runtime.InteropServices.GCHandle.Alloc(component, System.Runtime.InteropServices.GCHandleType.Pinned);
            try
            {
                System.Runtime.InteropServices.Marshal.Copy(handle.AddrOfPinnedObject(), bytes, 0, size);
            }
            finally
            {
                handle.Free();
            }
            return $"[{size}B] {BitConverter.ToString(bytes)}";
        }

        return component.ToString() ?? "null";
    }

    /// <summary>
    /// Diffs two serialized states (local vs server) and logs only the differences.
    /// </summary>
    public static void LogStateDiff(string label, long tick, byte[] localData, byte[] serverData)
    {
        try
        {
            var localWorld = new EntityWorld();
            StateSerializer.Deserialize(localWorld, localData);

            var serverWorld = new EntityWorld();
            StateSerializer.Deserialize(serverWorld, serverData);

            var sb = new StringBuilder();
            sb.AppendLine($"[{label}] State diff at Tick {tick}:");

            int maxEntities = Math.Max(localWorld._nextEntityId, serverWorld._nextEntityId);
            int maxTypes = Math.Max(localWorld._componentStores.Length, serverWorld._componentStores.Length);
            int diffCount = 0;

            for (int i = 0; i < maxEntities; i++)
            {
                var localMask = i < localWorld.EntityMasks.Length ? localWorld.EntityMasks[i] : default;
                var serverMask = i < serverWorld.EntityMasks.Length ? serverWorld.EntityMasks[i] : default;

                bool localExists = !localMask.IsEmpty;
                bool serverExists = !serverMask.IsEmpty;

                if (!localExists && !serverExists) continue;

                if (localExists != serverExists)
                {
                    sb.AppendLine($"  Entity {i}: {(localExists ? "LOCAL only" : "SERVER only")}");
                    diffCount++;
                    continue;
                }

                // Both exist — compare components
                for (int typeId = 0; typeId < maxTypes; typeId++)
                {
                    bool localHas = localMask.IsSet(typeId);
                    bool serverHas = serverMask.IsSet(typeId);

                    if (!localHas && !serverHas) continue;

                    string typeName = ComponentId.TryGetType(new DenseComponentId(typeId), out var type) && type != null
                        ? type.Name
                        : $"TypeId({typeId})";

                    if (localHas != serverHas)
                    {
                        sb.AppendLine($"  Entity {i}.{typeName}: {(localHas ? "LOCAL only" : "SERVER only")}");
                        diffCount++;
                        continue;
                    }

                    // Both have component — compare bytes
                    var localStore = typeId < localWorld._componentStores.Length ? localWorld._componentStores[typeId] : null;
                    var serverStore = typeId < serverWorld._componentStores.Length ? serverWorld._componentStores[typeId] : null;

                    if (localStore == null || serverStore == null) continue;

                    byte[] localBytes = localStore.SerializePacked(i + 1);
                    byte[] serverBytes = serverStore.SerializePacked(i + 1);

                    int elemSize = localStore.ElementSize;
                    int localOffset = i * elemSize;
                    int serverOffset = i * elemSize;

                    if (localOffset + elemSize > localBytes.Length || serverOffset + elemSize > serverBytes.Length)
                        continue;

                    bool same = true;
                    for (int b = 0; b < elemSize; b++)
                    {
                        if (localBytes[localOffset + b] != serverBytes[serverOffset + b])
                        {
                            same = false;
                            break;
                        }
                    }

                    if (!same)
                    {
                        var localHex = BitConverter.ToString(localBytes, localOffset, elemSize);
                        var serverHex = BitConverter.ToString(serverBytes, serverOffset, elemSize);
                        sb.AppendLine($"  Entity {i}.{typeName}:");
                        sb.AppendLine($"    LOCAL:  {localHex}");
                        sb.AppendLine($"    SERVER: {serverHex}");
                        diffCount++;
                    }
                }
            }

            if (diffCount == 0)
            {
                sb.AppendLine("  No component-level differences found (diff may be in entity masks or metadata)");
            }

            ILogger.LogWarning(sb.ToString());
        }
        catch (Exception ex)
        {
            ILogger.LogError($"[{label}] Failed to diff states at Tick {tick}: {ex.Message}");
        }
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
