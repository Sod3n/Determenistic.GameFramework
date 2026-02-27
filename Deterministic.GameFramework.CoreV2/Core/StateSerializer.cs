using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Deterministic.GameFramework.CoreV2;

public static class StateSerializer
{
    private const ushort CONST_VERSION = 1;
    private const int MAX_ARRAY_SIZE = 1_000_000; // Sanity check

    // Delegates for fast, type-safe array access without GCHandle
    private delegate int SerializeComponentDelegate(Array array, Span<byte> destination);
    private delegate void DeserializeComponentDelegate(Array array, ReadOnlySpan<byte> source);

    private static SerializeComponentDelegate?[] _serializers = new SerializeComponentDelegate?[128];
    private static DeserializeComponentDelegate?[] _deserializers = new DeserializeComponentDelegate?[128];

    public static byte[] Serialize(GlobalState state)
    {
        int maskSize = Unsafe.SizeOf<BitMask128>();
        
        // 1. Calculate total size
        // Header: Version(2) + NextEntityId (4) + EntityMasks Length (4) + EntityMasks Bytes
        int totalSize = 2 + 4 + 4 + (state._entityMasks.Length * maskSize);
        
        // Components: Count (4)
        totalSize += 4;
        
        var activeComponents = new List<int>();
        for (int i = 0; i < state._componentArrays.Length; i++)
        {
            if (state._componentArrays[i] != null)
            {
                activeComponents.Add(i);
                // ID (4) + Length (4) + Bytes
                totalSize += 4 + 4 + (state._componentArrays[i].Length * state._componentElementSizes[i]);
            }
        }

        byte[] buffer = new byte[totalSize];
        var span = new Span<byte>(buffer);
        int offset = 0;

        // 2. Write Header
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(offset), CONST_VERSION);
        offset += 2;

        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(offset), state._nextEntityId);
        offset += 4;

        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(offset), state._entityMasks.Length);
        offset += 4;

        var masksSpan = MemoryMarshal.AsBytes(new ReadOnlySpan<BitMask128>(state._entityMasks));
        masksSpan.CopyTo(span.Slice(offset));
        offset += masksSpan.Length;

        // 3. Write Components
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(offset), activeComponents.Count);
        offset += 4;

        foreach (var typeId in activeComponents)
        {
            var array = state._componentArrays[typeId];
            int elementSize = state._componentElementSizes[typeId];
            
            // TypeID (NetworkId)
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(offset), typeId);
            offset += 4;

            // Array Length (Capacity)
            BinaryPrimitives.WriteInt32LittleEndian(span.Slice(offset), array.Length);
            offset += 4;

            // Data
            var serializer = GetSerializer(typeId, state._componentTypes[typeId]);
            int writtenBytes = serializer(array, span.Slice(offset));
            
            offset += writtenBytes;
        }

        return buffer;
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

        int maskLength = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset));
        offset += 4;

        if (maskLength > MAX_ARRAY_SIZE) throw new Exception("EntityMask array too large. Possible corruption.");

        // Force new array allocation to ensure 100% clean slate
        state._entityMasks = new BitMask128[maskLength];

        int maskSize = Unsafe.SizeOf<BitMask128>();
        int maskByteLength = maskLength * maskSize;
        var maskSpan = MemoryMarshal.AsBytes(new Span<BitMask128>(state._entityMasks));
        span.Slice(offset, maskByteLength).CopyTo(maskSpan);
        offset += maskByteLength;

        // 3. Read Components
        int componentCount = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset));
        offset += 4;

        for (int i = 0; i < componentCount; i++)
        {
            int typeId = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset));
            offset += 4;

            int arrayLength = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(offset));
            offset += 4;

            if (arrayLength > MAX_ARRAY_SIZE) throw new Exception($"Component Array {typeId} too large ({arrayLength}). Possible corruption.");

            // Ensure we have the array info
            state.EnsureTypedCapacityInternal(typeId);

            // Restore Array
            Type type = state._componentTypes[typeId];
            if (type == null)
            {
                // Fallback: If we have an existing array, use its type.
                if (state._componentArrays[typeId] != null)
                {
                    type = state._componentArrays[typeId].GetType().GetElementType()!;
                    state._componentTypes[typeId] = type;
                    state._componentElementSizes[typeId] = Marshal.SizeOf(type);
                }
                else
                {
                     throw new Exception($"Cannot deserialize Component ID {typeId}: Type is unknown. Ensure all components are registered or accessed at least once before deserialization.");
                }
            }

            int elementSize = state._componentElementSizes[typeId];
            int dataByteLength = arrayLength * elementSize;

            if (state._componentArrays[typeId] == null || state._componentArrays[typeId].Length != arrayLength)
            {
                state._componentArrays[typeId] = Array.CreateInstance(type, arrayLength);
            }

            // Copy back
            var deserializer = GetDeserializer(typeId, type);
            deserializer(state._componentArrays[typeId], span.Slice(offset, dataByteLength));
            
            offset += dataByteLength;
        }
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
