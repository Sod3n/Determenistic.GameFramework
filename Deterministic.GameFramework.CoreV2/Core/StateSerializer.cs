using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Deterministic.GameFramework.CoreV2;

public static class StateSerializer
{
    private const ushort CONST_VERSION = 2; // Bumped version for new format
    private const int MAX_ARRAY_SIZE = 1_000_000; // Sanity check

    // Delegates for fast, type-safe array access without GCHandle
    private delegate int SerializeComponentDelegate(Array array, Span<byte> destination);
    private delegate void DeserializeComponentDelegate(Array array, ReadOnlySpan<byte> source);

    private static SerializeComponentDelegate?[] _serializers = new SerializeComponentDelegate?[128];
    private static DeserializeComponentDelegate?[] _deserializers = new DeserializeComponentDelegate?[128];

    public readonly struct PooledBuffer : IDisposable
    {
        public readonly byte[] Array;
        public readonly int Length;

        public PooledBuffer(byte[] array, int length)
        {
            Array = array;
            Length = length;
        }

        public void Dispose()
        {
            ArrayPool<byte>.Shared.Return(Array);
        }
        
        public Span<byte> Span => new Span<byte>(Array, 0, Length);
    }

    public static PooledBuffer SerializePooled(GlobalState state)
    {
        int totalSize = CalculateSize(state, out int entityCapacity, out int presenceByteCount, out var activeComponents);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(totalSize);
        SerializeInternal(state, buffer, totalSize, entityCapacity, presenceByteCount, activeComponents);
        return new PooledBuffer(buffer, totalSize);
    }

    public static byte[] Serialize(GlobalState state)
    {
        int totalSize = CalculateSize(state, out int entityCapacity, out int presenceByteCount, out var activeComponents);
        byte[] buffer = new byte[totalSize];
        SerializeInternal(state, buffer, totalSize, entityCapacity, presenceByteCount, activeComponents);
        return buffer;
    }

    private static int CalculateSize(GlobalState state, out int entityCapacity, out int presenceByteCount, out int activeComponentCount)
    {
        // 1. Calculate total size
        // Header: Version(2) + NextEntityId (4) + EntityCapacity (4)
        int totalSize = 2 + 4 + 4;
        
        // Components: Count (4)
        totalSize += 4;
        
        // External State: Count (4)
        totalSize += 4;
        foreach (var kvp in state.ExternalState)
        {
            // Key Length (4) + Key Bytes + Value Length (4) + Value Bytes
            totalSize += 4 + System.Text.Encoding.UTF8.GetByteCount(kvp.Key);
            totalSize += 4 + kvp.Value.Length;
        }
        
        entityCapacity = state._entityMasks.Length;
        presenceByteCount = (entityCapacity + 7) / 8;
        activeComponentCount = 0;

        for (int i = 0; i < state._componentArrays.Length; i++)
        {
            if (state._componentArrays[i] != null)
            {
                activeComponentCount++;
                // ID (4) + Array Length (4) + Presence Length (4) + Presence Bytes + Data Bytes
                totalSize += 4 + 4 + 4 + presenceByteCount + (state._componentArrays[i].Length * state._componentElementSizes[i]);
            }
        }
        return totalSize;
    }

    private static void SerializeInternal(GlobalState state, byte[] buffer, int totalSize, int entityCapacity, int presenceByteCount, int activeComponentCount)
    {
        var span = new Span<byte>(buffer, 0, totalSize);
        int offset = 0;

        // 2. Write Header
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(offset), CONST_VERSION);
        offset += 2;

        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(offset), state._nextEntityId);
        offset += 4;

        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(offset), entityCapacity);
        offset += 4;
        
        // 2.5 Write External State
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(offset), state.ExternalState.Count);
        offset += 4;
        
        foreach (var kvp in state.ExternalState)
        {
            // Key
            byte[] keyBytes = System.Text.Encoding.UTF8.GetBytes(kvp.Key);
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(offset), keyBytes.Length);
            offset += 4;
            
            keyBytes.CopyTo(span.Slice(offset));
            offset += keyBytes.Length;
            
            // Value
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(offset), kvp.Value.Length);
            offset += 4;
            
            kvp.Value.CopyTo(span.Slice(offset));
            offset += kvp.Value.Length;
        }

        // 3. Write Components
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(offset), activeComponentCount);
        offset += 4;

        for (int denseId = 0; denseId < state._componentArrays.Length; denseId++)
        {
            if (state._componentArrays[denseId] == null) continue;

            var array = state._componentArrays[denseId];
            
            // Write DenseId directly (Handshake ensures consistency)
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(offset), denseId);
            offset += 4;

            // Array Length (Capacity)
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(offset), array.Length);
            offset += 4;

            // Write Presence Mask
            // We need to know which entities actually HAVE this component enabled in their mask
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(offset), presenceByteCount);
            offset += 4;

            // Clear presence bytes in buffer before writing ORed bits
            span.Slice(offset, presenceByteCount).Clear();

            for (int e = 0; e < entityCapacity; e++)
            {
                if (state._entityMasks[e].IsSet(denseId))
                {
                    buffer[offset + (e / 8)] |= (byte)(1 << (e % 8));
                }
            }
            offset += presenceByteCount;

            // Data
            var serializer = GetSerializer(denseId, state._componentTypes[denseId]);
            int writtenBytes = serializer(array, span.Slice(offset));
            
            offset += writtenBytes;
        }
    }

    public static void Deserialize(GlobalState state, byte[] buffer)
    {
        var span = new ReadOnlySpan<byte>(buffer);
        int offset = 0;

        // 1. Check Version
        ushort version = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(offset));
        offset += 2;

        if (version != CONST_VERSION)
        {
            throw new Exception($"SaveState Version Mismatch. Expected {CONST_VERSION}, got {version}");
        }

        // 2. Read Header
        state._nextEntityId = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset));
        offset += 4;

        int entityCapacity = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset));
        offset += 4;
        
        Console.WriteLine($"[StateSerializer] Header: Version={version}, NextEntityId={state._nextEntityId}, EntityCapacity={entityCapacity}, Offset={offset}");

        if (entityCapacity > MAX_ARRAY_SIZE) throw new Exception("EntityMask array too large. Possible corruption.");

        // Resize or Reallocate EntityMasks
        if (state._entityMasks == null || state._entityMasks.Length < entityCapacity)
        {
            state._entityMasks = new BitMask128[entityCapacity];
        }
        else
        {
            // Clear existing masks for reuse
            Array.Clear(state._entityMasks, 0, state._entityMasks.Length);
        }
        
        // Note: No need to read raw masks anymore, we rebuild them from components

        // 2.5 Read External State
        int externalStateCount = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset));
        offset += 4;
        
        Console.WriteLine($"[StateSerializer] ExternalState Count: {externalStateCount}, Offset={offset}");

        state.ExternalState.Clear();
        for (int i = 0; i < externalStateCount; i++)
        {
            // Key
            int keyLength = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset));
            offset += 4;
            
            string key = System.Text.Encoding.UTF8.GetString(span.Slice(offset, keyLength));
            offset += keyLength;
            
            // Value
            int valueLength = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset));
            offset += 4;
            
            Console.WriteLine($"[StateSerializer] ExternalState[{i}]: Key='{key}' (len={keyLength}), ValueLen={valueLength}, Offset={offset}");

            byte[] value = span.Slice(offset, valueLength).ToArray();
            offset += valueLength;
            
            state.ExternalState[key] = value;
        }

        // 3. Read Components
        int componentCount = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset));
        offset += 4;

        Console.WriteLine($"[StateSerializer] Component Count: {componentCount}, Offset={offset}");

        for (int i = 0; i < componentCount; i++)
        {
            int denseId = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset));
            offset += 4;

            int arrayLength = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset));
            offset += 4;
            
            int presenceByteCount = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset));
            offset += 4;

            Console.WriteLine($"[StateSerializer] Component[{i}]: DenseId={denseId}, ArrayLen={arrayLength}, PresenceBytes={presenceByteCount}, Offset={offset}");

            if (arrayLength > MAX_ARRAY_SIZE) throw new Exception($"Component Array for DenseId {denseId} too large ({arrayLength}). Possible corruption.");

            // Verify presenceByteCount validity to debug IndexOutOfRangeException
            int expectedPresenceBytes = (entityCapacity + 7) / 8;
            if (presenceByteCount < expectedPresenceBytes)
            {
                Console.WriteLine($"[StateSerializer] ERROR: Component DenseId {denseId}: presenceByteCount {presenceByteCount} is too small for entityCapacity {entityCapacity}. Expected at least {expectedPresenceBytes}. Offset: {offset}");
            }

            // Read Presence Mask and Apply to EntityMasks
            var presenceSpan = span.Slice(offset, presenceByteCount);
            for (int e = 0; e < entityCapacity; e++)
            {
                if ((presenceSpan[e / 8] & (1 << (e % 8))) != 0)
                {
                    state._entityMasks[e].Set(denseId);
                }
            }
            offset += presenceByteCount;

            // Ensure we have the array info
            state.EnsureTypedCapacityInternal(denseId);

            // Restore Array
            Type type = state._componentTypes[denseId];
            if (type == null)
            {
                // Fallback: If we have an existing array, use its type.
                if (state._componentArrays[denseId] != null)
                {
                    type = state._componentArrays[denseId].GetType().GetElementType()!;
                    state._componentTypes[denseId] = type;
                    state._componentElementSizes[denseId] = GetManagedSize(type);
                }
                else
                {
                     // This can happen if we have data for a component that isn't registered locally yet.
                     // But since we are using DenseId, it implies strict synchronization.
                     throw new Exception($"Cannot deserialize Component DenseId {denseId}: Type is unknown. Ensure ComponentTypeRegistry is synced.");
                }
            }

            int elementSize = state._componentElementSizes[denseId];
            int dataByteLength = arrayLength * elementSize;

            if (state._componentArrays[denseId] == null || state._componentArrays[denseId].Length != arrayLength)
            {
                state._componentArrays[denseId] = Array.CreateInstance(type, arrayLength);
            }

            // Copy back
            var deserializer = GetDeserializer(denseId, type);
            deserializer(state._componentArrays[denseId], span.Slice(offset, dataByteLength));
            
            offset += dataByteLength;
        }
    }

    private static int GetManagedSize(Type type)
    {
        var method = typeof(Unsafe).GetMethod("SizeOf")!.MakeGenericMethod(type);
        return (int)method.Invoke(null, null)!;
    }

    private static SerializeComponentDelegate GetSerializer(int typeId, Type type)
    {
        if (typeId >= _serializers.Length)
        {
            Array.Resize(ref _serializers, Math.Max(_serializers.Length * 2, typeId + 1));
        }

        if (_serializers[typeId] == null)
        {
            _serializers[typeId] = CreateSerializer(type);
        }
        return _serializers[typeId]!;
    }

    private static DeserializeComponentDelegate GetDeserializer(int typeId, Type type)
    {
        if (typeId >= _deserializers.Length)
        {
            Array.Resize(ref _deserializers, Math.Max(_deserializers.Length * 2, typeId + 1));
        }

        if (_deserializers[typeId] == null)
        {
            _deserializers[typeId] = CreateDeserializer(type);
        }
        return _deserializers[typeId]!;
    }

    private static SerializeComponentDelegate CreateSerializer(Type type)
    {
        // Use reflection to create a typed delegate: (Array arr, Span<byte> dest) => ...
        var method = typeof(StateSerializer).GetMethod(nameof(SerializeTyped), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(type);
        return (SerializeComponentDelegate)Delegate.CreateDelegate(typeof(SerializeComponentDelegate), method);
    }

    private static DeserializeComponentDelegate CreateDeserializer(Type type)
    {
        var method = typeof(StateSerializer).GetMethod(nameof(DeserializeTyped), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(type);
        return (DeserializeComponentDelegate)Delegate.CreateDelegate(typeof(DeserializeComponentDelegate), method);
    }

    private static int SerializeTyped<T>(Array array, Span<byte> destination) where T : struct
    {
        var tArray = (T[])array;
        var sourceBytes = MemoryMarshal.AsBytes(new ReadOnlySpan<T>(tArray));
        sourceBytes.CopyTo(destination);
        return sourceBytes.Length;
    }

    private static void DeserializeTyped<T>(Array array, ReadOnlySpan<byte> source) where T : struct
    {
        var tArray = (T[])array;
        // MemoryMarshal.Cast allows us to view the bytes as T structs
        MemoryMarshal.Cast<byte, T>(source).CopyTo(new Span<T>(tArray));
    }
}
