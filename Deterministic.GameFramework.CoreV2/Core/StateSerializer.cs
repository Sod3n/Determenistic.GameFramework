using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using MessagePack;

namespace Deterministic.GameFramework.CoreV2;

public static class StateSerializer
{
    [ThreadStatic]
    private static StateSnapshot? _serializeCache;

    private static StateSnapshot GetSerializeCache()
    {
        if (_serializeCache == null)
        {
            _serializeCache = new StateSnapshot();
        }
        _serializeCache.Clear();
        return _serializeCache;
    }

    public static byte[] Serialize(GlobalState state)
    {
        // 1. Get Pooled Snapshot
        var snapshot = GetSerializeCache();
        snapshot.NextEntityId = state._nextEntityId;
        snapshot.EntityCapacity = state._entityMasks.Length;
        
        // External State
        foreach (var kvp in state.ExternalState)
        {
            snapshot.ExternalState[kvp.Key] = kvp.Value;
        }

        // 2. Mappings
        var mappings = ComponentIdSerializer.GetMappingsSnapshot();
        foreach (var kvp in mappings)
        {
            snapshot.Mappings.Add(new MappingSnapshot
            {
                StableId = kvp.Key.Value.ToByteArray(),
                DenseId = kvp.Value.Value
            });
        }

        // 3. Entity Masks (Global Presence)
        // Serialize the entire mask array in one go
        int maskElementSize = 16; // BitMask128 is 2 ulongs (16 bytes)
        snapshot.EntityMasks = MemoryHelper.SerializeArrayUntyped(state._entityMasks, maskElementSize);

        // 4. Components
        for (int localId = 0; localId < state._componentArrays.Length; localId++)
        {
            var array = state._componentArrays[localId];
            if (array == null) continue;

            int elementSize = state._componentElementSizes[localId];
            if (elementSize == 0) continue; 
            
            // Serialize Data (Untyped Fast Copy - Zero Alloc Handles)
            byte[] data = MemoryHelper.SerializeArrayUntyped(array, elementSize);
            
            snapshot.Components.Add(new ComponentSnapshot
            {
                TypeId = localId,
                Data = data,
                Count = array.Length
            });
        }

        // 5. Serialize with MessagePack
        return MessagePackSerializer.Serialize(snapshot);
    }

    public static void Deserialize(GlobalState state, byte[] buffer, bool syncComponentIds = true, bool autoReset = true)
    {
        // 1. Deserialize Snapshot
        var snapshot = MessagePackSerializer.Deserialize<StateSnapshot>(buffer);

        // 2. Reset State
        if (autoReset)
        {
            state.ResetComponents(clearCache: syncComponentIds);
        }

        // 3. Apply Header
        state._nextEntityId = snapshot.NextEntityId;
        int entityCapacity = snapshot.EntityCapacity;
        
        // Ensure EntityMasks capacity
        if (state._entityMasks == null || state._entityMasks.Length < entityCapacity)
        {
            state._entityMasks = new BitMask128[entityCapacity];
        }
        
        // Apply Entity Masks
        if (!snapshot.EntityMasks.IsEmpty)
        {
            // If the serialized masks are smaller/larger than current capacity, we might need to handle it.
            // But usually we trust entityCapacity from snapshot.
            // Ensure capacity matches snapshot claim
            if (state._entityMasks.Length < entityCapacity)
            {
                 Array.Resize(ref state._entityMasks, entityCapacity);
            }
            
            // Direct copy
            int maskElementSize = 16;
            MemoryHelper.DeserializeArrayUntyped(snapshot.EntityMasks, state._entityMasks, maskElementSize);
        }
        else
        {
            // Fallback clear if missing (shouldn't happen in new format)
            Array.Clear(state._entityMasks, 0, state._entityMasks.Length);
        }

        // 4. Apply Mappings
        if (syncComponentIds && snapshot.Mappings != null)
        {
            global::Deterministic.GameFramework.CoreV2.ComponentId.ClearMappings();
            foreach (var mapping in snapshot.Mappings)
            {
                var stableId = new StableComponentId(new Guid(mapping.StableId.ToArray()));
                var denseId = new DenseComponentId(mapping.DenseId);
                global::Deterministic.GameFramework.CoreV2.ComponentId.RegisterMapping(stableId, denseId);
            }
        }

        // 5. Apply External State
        state.ExternalState.Clear();
        if (snapshot.ExternalState != null)
        {
            foreach (var kvp in snapshot.ExternalState)
            {
                state.ExternalState[kvp.Key] = kvp.Value;
            }
        }

        // 6. Apply Components
        if (snapshot.Components != null)
        {
            foreach (var compSnapshot in snapshot.Components)
            {
                int localId = compSnapshot.TypeId;
                
                state.EnsureTypedCapacityInternal(localId);
                Type? type = state._componentTypes[localId];
                if (type == null)
                {
                     throw new Exception($"Cannot deserialize Component LocalId {localId}: Type is unknown.");
                }

                if (state._componentArrays[localId] == null || state._componentArrays[localId]!.Length != compSnapshot.Count)
                {
                     state._componentArrays[localId] = Array.CreateInstance(type, compSnapshot.Count);
                     // Element size is already set by EnsureTypedCapacityInternal
                }

                MemoryHelper.DeserializeArrayUntyped(compSnapshot.Data, state._componentArrays[localId]!, state._componentElementSizes[localId]);
            }
        }
    }

}
