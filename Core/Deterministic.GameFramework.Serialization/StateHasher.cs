using System;
using System.Security.Cryptography;
using Deterministic.GameFramework.ECS;

namespace Deterministic.GameFramework.Serialization;

public static class StateHasher
{
    public static Guid Hash(EntityWorld state)
    {
        byte[] data = StateSerializer.Serialize(state);
        return Hash(data);
    }

    public static Guid Hash(byte[] data)
    {
#if NET5_0_OR_GREATER
        Span<byte> hashBytes = stackalloc byte[16];
        MD5.HashData(data, hashBytes);
        return new Guid(hashBytes);
#else
        using var md5 = MD5.Create();
        byte[] hashBytes = md5.ComputeHash(data);
        return new Guid(hashBytes);
#endif
    }
}
