using System.Linq;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Types;
using Deterministic.GameFramework.Utils;
using Guid = System.Guid;

namespace Deterministic.GameFramework.Serialization;

public static class StateSerializer
{
    public static void AdoptMappingsFrom(byte[] buffer)
    {
        using var ms = new MemoryStream(buffer);
        using var reader = new BinaryReader(ms);

        reader.ReadInt32();
        reader.ReadInt32();

        int extCount = reader.ReadInt32();
        for (int i = 0; i < extCount; i++)
        {
            reader.ReadString();
            int len = reader.ReadInt32();
            reader.ReadBytes(len);
        }

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

    [ThreadStatic] private static MemoryStream? _sharedStream;

    public static (byte[] Buffer, int Length) SerializeInto(EntityWorld state)
    {
        var ms = _sharedStream ??= new MemoryStream(1024 * 1024);
        ms.Position = 0;
        ms.SetLength(0);

        var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true);

        BitMask128 activeTypes = new();
        for (int i = 0; i < state._nextEntityId; i++)
        {
            activeTypes.Or(state._entityMasks[i]);
        }

        writer.Write(state._nextEntityId);
        writer.Write(state._nextEntityId);

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

        var allMappings = ComponentIdSerializer.GetMappingsSnapshot();
        writer.Write(allMappings.Count);
        foreach (var kvp in allMappings)
        {
            writer.Write(kvp.Key.Value.ToByteArray());
            writer.Write(kvp.Value.Value);
        }

        int maskElementSize = 16;
        byte[] maskData = MemoryHelper.SerializeArrayUntyped(state._entityMasks, maskElementSize, state._nextEntityId);
        writer.Write(maskData.Length);
        writer.Write(maskData);

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

            byte[] data = store.SerializePacked(state._nextEntityId);

            writer.Write(localId);
            writer.Write(data.Length);
            writer.Write(data);
            writer.Write(state._nextEntityId);
        }

        writer.Flush();
        return (ms.GetBuffer(), (int)ms.Length);
    }

    public static byte[] Serialize(EntityWorld state)
    {
        var (buf, len) = SerializeInto(state);
        var result = new byte[len];
        Buffer.BlockCopy(buf, 0, result, 0, len);
        return result;
    }

    public static void Deserialize(EntityWorld state, byte[] buffer, int length, bool syncComponentIds = true, bool autoReset = true, bool invalidateSystemData = true, bool fullInvalidate = false)
    {
        using var ms = new MemoryStream(buffer, 0, length, writable: false);
        using var reader = new BinaryReader(ms);
        DeserializeFromReader(state, reader, syncComponentIds, autoReset, invalidateSystemData, fullInvalidate);
    }

    public static void Deserialize(EntityWorld state, byte[] buffer, bool syncComponentIds = true, bool autoReset = true, bool invalidateSystemData = true, bool fullInvalidate = false)
    {
        using var ms = new MemoryStream(buffer);
        using var reader = new BinaryReader(ms);
        DeserializeFromReader(state, reader, syncComponentIds, autoReset, invalidateSystemData, fullInvalidate);
    }

    private static void DeserializeFromReader(EntityWorld state, BinaryReader reader, bool syncComponentIds, bool autoReset, bool invalidateSystemData, bool fullInvalidate)
    {
        int nextEntityId = reader.ReadInt32();
        int entityCapacity = reader.ReadInt32();

        if (autoReset)
        {
            state.ResetComponents(clearCache: syncComponentIds);
        }

        state._nextEntityId = nextEntityId;

        if (state._entityMasks == null || state._entityMasks.Length != entityCapacity)
        {
            state._entityMasks = new BitMask128[entityCapacity];
        }

        state.ExternalState.Clear();
        int extCount = reader.ReadInt32();
        for (int i = 0; i < extCount; i++)
        {
            string key = reader.ReadString();
            int len = reader.ReadInt32();
            byte[] val = reader.ReadBytes(len);
            state.ExternalState[key] = val;
        }

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
            for (int i = 0; i < mapCount; i++)
            {
                byte[] guidBytes = reader.ReadBytes(16);
                int serverLocalId = reader.ReadInt32();
                var stableId = new StableComponentId(new Guid(guidBytes));

                if (ComponentId.TryGetDense(stableId, out var clientDenseId))
                {
                    int clientLocalId = clientDenseId.Value;
                    if (serverLocalId != clientLocalId)
                    {
                        if (remapTable == null)
                        {
                            remapTable = new int[128];
                            for (int j = 0; j < remapTable.Length; j++)
                                remapTable[j] = j;
                        }
                        if (serverLocalId < remapTable.Length)
                            remapTable[serverLocalId] = clientLocalId;
                    }
                }
            }
        }

        int maskDataLen = reader.ReadInt32();
        byte[] maskData = reader.ReadBytes(maskDataLen);
        if (maskData.Length > 0)
        {
            int maskElementSize = 16;
            MemoryHelper.DeserializeArrayUntyped(maskData, state._entityMasks, maskElementSize);

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

        int compCount = reader.ReadInt32();
        for (int i = 0; i < compCount; i++)
        {
            int serverLocalId = reader.ReadInt32();
            int dataLen = reader.ReadInt32();
            byte[] data = reader.ReadBytes(dataLen);
            int elemCount = reader.ReadInt32();

            int localId = serverLocalId;
            if (remapTable != null && serverLocalId < remapTable.Length)
                localId = remapTable[serverLocalId];

            state.EnsureTypedCapacityInternal(localId);
            Type? type = state._componentTypes[localId];
            if (type == null)
            {
                if (remapTable != null)
                    continue;
                throw new Exception($"Cannot deserialize Component LocalId {localId}: Type is unknown.");
            }

            state.EnsureStoreFromType(localId, type, elemCount);
            state._componentStores[localId]!.DeserializePacked(data, elemCount);
        }

        if (invalidateSystemData)
            state.InvalidateDerivedState(full: fullInvalidate);
    }

}
