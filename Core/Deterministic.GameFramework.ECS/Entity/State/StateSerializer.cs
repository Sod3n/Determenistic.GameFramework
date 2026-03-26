using System.Linq;
using Deterministic.GameFramework.Types;
using Deterministic.GameFramework.Utils;
using Guid = System.Guid;

namespace Deterministic.GameFramework.ECS;

public static class StateSerializer
{
    /// <summary>
    /// Adopt the component ID mappings (StableId → LocalId) from a serialized state.
    /// Clears the current LocalId assignments and replaces them with the ones from
    /// the serialized data. Type registrations from RegisterAssembly are preserved.
    /// Call this before Deserialize to ensure client and server use identical LocalIds.
    /// </summary>
    public static void AdoptMappingsFrom(byte[] buffer)
    {
        using var ms = new MemoryStream(buffer);
        using var reader = new BinaryReader(ms);

        // Skip header
        reader.ReadInt32(); // nextEntityId
        reader.ReadInt32(); // entityCapacity

        // Skip external state
        int extCount = reader.ReadInt32();
        for (int i = 0; i < extCount; i++)
        {
            reader.ReadString();
            int len = reader.ReadInt32();
            reader.ReadBytes(len);
        }

        // Read mappings and apply the server's LocalId assignments.
        // Don't clear existing mappings — the server only serializes active
        // components, so inactive ones (like PlayerEntity before any player
        // joins) would be lost. Instead, overlay server assignments on top.
        int mapCount = reader.ReadInt32();

        for (int i = 0; i < mapCount; i++)
        {
            byte[] guidBytes = reader.ReadBytes(16);
            int denseIdVal = reader.ReadInt32();
            var stableId = new StableComponentId(new Guid(guidBytes));
            var denseId = new DenseComponentId(denseIdVal);
            ComponentId.RegisterMapping(stableId, denseId);
        }
    }

    public static byte[] Serialize(EntityWorld state)
    {
        // 0. Determine active component types by ORing all entity masks
        BitMask128 activeTypes = new();
        for (int i = 0; i < state._nextEntityId; i++)
        {
            activeTypes.Or(state._entityMasks[i]);
        }

        using (var ms = new MemoryStream())
        using (var writer = new BinaryWriter(ms))
        {
            // 1. Header
            writer.Write(state._nextEntityId);
            writer.Write(state._nextEntityId);

            // 2. External State
            writer.Write(state.ExternalState.Count);

            var sortedKeys = new System.Collections.Generic.List<string>(state.ExternalState.Keys);
            sortedKeys.Sort(StringComparer.Ordinal);

            foreach (var key in sortedKeys)
            {
                var val = state.ExternalState[key];
                writer.Write(key);
                writer.Write(val.Length);
                writer.Write(val);
            }

            // 3. Mappings (Only for active types to ensure stable hash)
            var allMappings = ComponentIdSerializer.GetMappingsSnapshot();
            var activeMappings = allMappings.Where(m => activeTypes.IsSet(m.Value.Value)).ToList();
            writer.Write(activeMappings.Count);
            foreach (var kvp in activeMappings)
            {
                writer.Write(kvp.Key.Value.ToByteArray()); // 16 bytes
                writer.Write(kvp.Value.Value); // int
            }

            // 4. Entity Masks (Only up to nextEntityId)
            int maskElementSize = 16; // BitMask128 is 2 ulongs (16 bytes)
            byte[] maskData = MemoryHelper.SerializeArrayUntyped(state._entityMasks, maskElementSize, state._nextEntityId);
            writer.Write(maskData.Length);
            writer.Write(maskData);

            // 5. Components (Only active types) — serialized tightly packed (no alignment padding)
            int validComponents = 0;
            for (int localId = 0; localId < state._componentStores.Length; localId++)
            {
                if (activeTypes.IsSet(localId) && state._componentStores[localId] != null && state._componentElementSizes[localId] > 0)
                    validComponents++;
            }
            writer.Write(validComponents);

            for (int localId = 0; localId < state._componentStores.Length; localId++)
            {
                if (!activeTypes.IsSet(localId)) continue;

                var store = state._componentStores[localId];
                if (store == null) continue;

                int elementSize = state._componentElementSizes[localId];
                if (elementSize == 0) continue;

                // Serialize tightly packed (strip alignment padding)
                byte[] data = store.SerializePacked(state._nextEntityId);

                writer.Write(localId);
                writer.Write(data.Length);
                writer.Write(data);
                writer.Write(state._nextEntityId);
            }

            return ms.ToArray();
        }
    }

    public static void Deserialize(EntityWorld state, byte[] buffer, bool syncComponentIds = true, bool autoReset = true)
    {
        using (var ms = new MemoryStream(buffer))
        using (var reader = new BinaryReader(ms))
        {
            // 1. Header
            int nextEntityId = reader.ReadInt32();
            int entityCapacity = reader.ReadInt32();

            // 2. Reset State
            if (autoReset)
            {
                state.ResetComponents(clearCache: syncComponentIds);
            }

            state._nextEntityId = nextEntityId;

            if (state._entityMasks == null || state._entityMasks.Length != entityCapacity)
            {
                state._entityMasks = new BitMask128[entityCapacity];
            }

            // 3. External State
            state.ExternalState.Clear();
            int extCount = reader.ReadInt32();
            for (int i = 0; i < extCount; i++)
            {
                string key = reader.ReadString();
                int len = reader.ReadInt32();
                byte[] val = reader.ReadBytes(len);
                state.ExternalState[key] = val;
            }

            // 4. Mappings
            // Build a remap table: serverLocalId → clientLocalId.
            // When syncComponentIds is true, the global mappings are replaced wholesale.
            // When false, we keep local registrations but still need to translate
            // server LocalIds (used in the serialized data) to client LocalIds.
            int mapCount = reader.ReadInt32();
            int[]? remapTable = null;

            if (syncComponentIds)
            {
                ComponentId.ClearMappings();
                for (int i = 0; i < mapCount; i++)
                {
                    byte[] guidBytes = reader.ReadBytes(16);
                    int denseIdVal = reader.ReadInt32();
                    var stableId = new StableComponentId(new Guid(guidBytes));
                    var denseId = new DenseComponentId(denseIdVal);
                    ComponentId.RegisterMapping(stableId, denseId);
                }
            }
            else
            {
                // Read the server's mappings and build a remap table
                // so we can translate server LocalIds to client LocalIds.
                for (int i = 0; i < mapCount; i++)
                {
                    byte[] guidBytes = reader.ReadBytes(16);
                    int serverLocalId = reader.ReadInt32();
                    var stableId = new StableComponentId(new Guid(guidBytes));

                    // Look up what LocalId the client uses for this StableId
                    if (ComponentId.TryGetDense(stableId, out var clientDenseId))
                    {
                        int clientLocalId = clientDenseId.Value;
                        if (serverLocalId != clientLocalId)
                        {
                            // Need remapping
                            if (remapTable == null)
                            {
                                remapTable = new int[128];
                                for (int j = 0; j < remapTable.Length; j++)
                                    remapTable[j] = j; // identity by default
                            }
                            if (serverLocalId < remapTable.Length)
                                remapTable[serverLocalId] = clientLocalId;
                        }
                    }
                    // If the client doesn't know this StableId, the component
                    // will be skipped during deserialization (type unknown).
                }
            }

            // 5. Entity Masks
            int maskDataLen = reader.ReadInt32();
            byte[] maskData = reader.ReadBytes(maskDataLen);
            if (maskData.Length > 0)
            {
                int maskElementSize = 16;
                MemoryHelper.DeserializeArrayUntyped(maskData, state._entityMasks, maskElementSize);

                // Remap bit positions in entity masks if LocalIds differ
                if (remapTable != null)
                {
                    for (int e = 0; e < nextEntityId; e++)
                    {
                        var oldMask = state._entityMasks[e];
                        if (oldMask.IsEmpty) continue;

                        var newMask = new BitMask128();
                        for (int bit = 0; bit < 128; bit++)
                        {
                            if (oldMask.IsSet(bit) && bit < remapTable.Length)
                                newMask.Set(remapTable[bit]);
                        }
                        state._entityMasks[e] = newMask;
                    }
                }
            }
            else
            {
                Array.Clear(state._entityMasks, 0, state._entityMasks.Length);
            }

            // 6. Components — deserialize from tightly packed format into aligned stores
            int compCount = reader.ReadInt32();
            for (int i = 0; i < compCount; i++)
            {
                int serverLocalId = reader.ReadInt32();
                int dataLen = reader.ReadInt32();
                byte[] data = reader.ReadBytes(dataLen);
                int elemCount = reader.ReadInt32();

                // Remap server LocalId to client LocalId if needed
                int localId = serverLocalId;
                if (remapTable != null && serverLocalId < remapTable.Length)
                    localId = remapTable[serverLocalId];

                state.EnsureTypedCapacityInternal(localId);
                Type? type = state._componentTypes[localId];
                if (type == null)
                {
                    if (remapTable != null)
                        continue; // Skip unknown components during cross-process sync
                    throw new Exception($"Cannot deserialize Component LocalId {localId}: Type is unknown.");
                }

                state.EnsureStoreFromType(localId, type, elemCount);
                state._componentStores[localId]!.DeserializePacked(data, elemCount);
            }
        }
    }

}
