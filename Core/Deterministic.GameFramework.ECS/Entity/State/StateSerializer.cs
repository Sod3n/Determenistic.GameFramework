using Deterministic.GameFramework.Types;
using Deterministic.GameFramework.Utils;
using Guid = System.Guid;

namespace Deterministic.GameFramework.ECS;

public static class StateSerializer
{
    public static byte[] Serialize(EntityWorld state)
    {
        using (var ms = new MemoryStream())
        using (var writer = new BinaryWriter(ms))
        {
            // 1. Header
            writer.Write(state._nextEntityId);
            writer.Write(state._entityMasks.Length); // Capacity

            // 2. External State
            writer.Write(state.ExternalState.Count);
            foreach (var kvp in state.ExternalState)
            {
                writer.Write(kvp.Key);
                writer.Write(kvp.Value.Length);
                writer.Write(kvp.Value);
            }

            // 3. Mappings
            var mappings = ComponentIdSerializer.GetMappingsSnapshot();
            writer.Write(mappings.Count);
            foreach (var kvp in mappings)
            {
                writer.Write(kvp.Key.Value.ToByteArray()); // 16 bytes
                writer.Write(kvp.Value.Value); // int
            }

            // 4. Entity Masks
            // Serialize the entire mask array in one go
            int maskElementSize = 16; // BitMask128 is 2 ulongs (16 bytes)
            byte[] maskData = MemoryHelper.SerializeArrayUntyped(state._entityMasks, maskElementSize);
            writer.Write(maskData.Length);
            writer.Write(maskData);

            // 5. Components
            // Count valid components first
            int validComponents = 0;
            for (int i = 0; i < state._componentArrays.Length; i++)
            {
                if (state._componentArrays[i] != null && state._componentElementSizes[i] > 0)
                    validComponents++;
            }
            writer.Write(validComponents);

            for (int localId = 0; localId < state._componentArrays.Length; localId++)
            {
                var array = state._componentArrays[localId];
                if (array == null) continue;

                int elementSize = state._componentElementSizes[localId];
                if (elementSize == 0) continue; 
                
                // Serialize Data
                byte[] data = MemoryHelper.SerializeArrayUntyped(array, elementSize);
                
                // Log large components
                if (data.Length > 10000)
                {
                    // Console.WriteLine($"[StateSerializer] Large Component Array: TypeId {localId}, Size {data.Length} bytes, Count {array.Length}, ElemSize {elementSize}");
                }

                writer.Write(localId);
                writer.Write(data.Length);
                writer.Write(data);
                writer.Write(array.Length); // Write count for array recreation
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
            
            // Ensure EntityMasks capacity
            if (state._entityMasks == null || state._entityMasks.Length < entityCapacity)
            {
                state._entityMasks = new BitMask128[entityCapacity];
            }
            else if (state._entityMasks.Length > entityCapacity)
            {
                 // Optional: shrink? usually we just grow.
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
            if (syncComponentIds && mapCount > 0)
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
                 // Skip mappings if not syncing
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
                // Ensure array size matches data if possible, or just copy what fits
                int maskElementSize = 16;
                // int count = maskData.Length / maskElementSize;
                // if (state._entityMasks.Length < count) Array.Resize(ref state._entityMasks, count);
                
                MemoryHelper.DeserializeArrayUntyped(maskData, state._entityMasks, maskElementSize);
            }
            else
            {
                Array.Clear(state._entityMasks, 0, state._entityMasks.Length);
            }

            // 6. Components
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

                if (state._componentArrays[localId] == null || state._componentArrays[localId]!.Length != elemCount)
                {
                     state._componentArrays[localId] = Array.CreateInstance(type, elemCount);
                }

                MemoryHelper.DeserializeArrayUntyped(data, state._componentArrays[localId]!, state._componentElementSizes[localId]);
            }
        }
    }

}
