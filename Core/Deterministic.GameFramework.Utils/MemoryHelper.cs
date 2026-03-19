using System;
using System.Runtime.CompilerServices;

namespace Deterministic.GameFramework.Utils;

public static class MemoryHelper
{
    /// <summary>
    /// Serializes a managed array of structs to a byte array using raw memory copy.
    /// This avoids GCHandle allocations and uses Unsafe pointer arithmetic.
    /// </summary>
    public static unsafe byte[] SerializeArrayUntyped(Array array, int elementSize)
    {
        if (array.Length == 0) return Array.Empty<byte>();

        int byteLength = array.Length * elementSize;
        byte[] data = new byte[byteLength];
        
        // Trick: Unsafe.As<byte[]> gives us a byte[] view of the object to satisfy fixed/GetArrayDataReference.
        // We cast the object reference directly.
        // This relies on all Arrays having the same header layout (MethodTable + Length).
        var fakeByteArr = Unsafe.As<byte[]>(array);
        
        fixed (byte* srcPtr = fakeByteArr)
        fixed (byte* destPtr = data)
        {
             Buffer.MemoryCopy(srcPtr, destPtr, byteLength, byteLength);
        }
        
        return data;
    }

    /// <summary>
    /// Deserializes raw bytes into a managed array of structs using raw memory copy.
    /// </summary>
    public static unsafe void DeserializeArrayUntyped(ReadOnlyMemory<byte> source, Array destination, int elementSize)
    {
        if (destination.Length == 0) return;

        var sourceSpan = source.Span;
        if (sourceSpan.IsEmpty) return;

        int copyBytes = Math.Min(sourceSpan.Length, destination.Length * elementSize);

        var fakeByteArr = Unsafe.As<byte[]>(destination);
        
        fixed (byte* destPtr = fakeByteArr)
        fixed (byte* srcPtr = sourceSpan)
        {
            Buffer.MemoryCopy(srcPtr, destPtr, copyBytes, copyBytes);
        }
    }
}
