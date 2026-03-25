using System.Linq;
using Deterministic.GameFramework.Types;
using Deterministic.GameFramework.Utils;
using Guid = System.Guid;

namespace Deterministic.GameFramework.ECS;

public static class StateSerializer
{
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
            int mapCount = reader.ReadInt32();
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
                for (int i = 0; i < mapCount; i++)
                {
                    reader.ReadBytes(16);
                    reader.ReadInt32();
                }
            }

            // 5. Entity Masks
            int maskDataLen = reader.ReadInt32();
            byte[] maskData = reader.ReadBytes(maskDataLen);
            if (maskData.Length > 0)
            {
                int maskElementSize = 16;
                MemoryHelper.DeserializeArrayUntyped(maskData, state._entityMasks, maskElementSize);
            }
            else
            {
                Array.Clear(state._entityMasks, 0, state._entityMasks.Length);
            }

            // 6. Components — deserialize from tightly packed format into aligned stores
            int compCount = reader.ReadInt32();
            for (int i = 0; i < compCount; i++)
            {
                int localId = reader.ReadInt32();
                int dataLen = reader.ReadInt32();
                byte[] data = reader.ReadBytes(dataLen);
                int elemCount = reader.ReadInt32();

                state.EnsureTypedCapacityInternal(localId);
                Type? type = state._componentTypes[localId];
                if (type == null)
                {
                     throw new Exception($"Cannot deserialize Component LocalId {localId}: Type is unknown.");
                }

                state.EnsureStoreFromType(localId, type, elemCount);
                state._componentStores[localId]!.DeserializePacked(data, elemCount);
            }
        }
    }

}
