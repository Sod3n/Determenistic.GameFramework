using System;
using System.Security.Cryptography;

namespace Deterministic.GameFramework.CoreV2;

public static class StateHasher
{
    public static Guid Hash(GlobalState state)
    {
        // Use StateSerializer to get the exact binary representation of the world
        // This ensures that what we sync and what we verify is identical.
        using var pooled = StateSerializer.SerializePooled(state);
        
        // Compute 128-bit hash (MD5 is fast enough and fits Guid)
#if NET5_0_OR_GREATER
        Span<byte> hashBytes = stackalloc byte[16];
        MD5.HashData(pooled.Span, hashBytes);
        return new Guid(hashBytes.ToArray());
#else
        using var md5 = MD5.Create();
        // pooled.Array is the underlying array, pooled.Length is the actual data length
        byte[] hashBytes = md5.ComputeHash(pooled.Array, 0, pooled.Length);
        return new Guid(hashBytes);
#endif
    }

    public static Guid Hash(byte[] data)
    {
#if NET5_0_OR_GREATER
        Span<byte> hashBytes = stackalloc byte[16];
        MD5.HashData(data, hashBytes);
        return new Guid(hashBytes.ToArray());
#else
        using var md5 = MD5.Create();
        byte[] hashBytes = md5.ComputeHash(data);
        return new Guid(hashBytes);
#endif
    }
}
