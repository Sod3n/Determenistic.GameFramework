using Deterministic.GameFramework.Network.Packets;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Network.Packets;

namespace Deterministic.GameFramework.Network.Tests;

public class NetworkActionHeaderTests
{
    [Fact]
    public void NetworkActionHeader_ShouldHaveCorrectSize()
    {
        int expectedSize = sizeof(int) + sizeof(int) + sizeof(long) + sizeof(int);
        int actualSize = Marshal.SizeOf<NetworkActionHeader>();
        
        actualSize.Should().Be(expectedSize);
    }

    [Fact]
    public void NetworkActionHeader_ShouldSerializeAndDeserialize_Correctly()
    {
        var header = new NetworkActionHeader
        {
            ComponentId = (DenseComponentId)42,
            TargetEntityId = 123,
            ExecuteTick = 9999L,
            DataLength = 256
        };

        Span<byte> buffer = stackalloc byte[Marshal.SizeOf<NetworkActionHeader>()];
#if NET8_0_OR_GREATER
        MemoryMarshal.Write(buffer, in header);
#else
        MemoryMarshal.Write(buffer, ref header);
#endif

#if NET8_0_OR_GREATER
        var deserialized = MemoryMarshal.Read<NetworkActionHeader>(buffer);
#else
        var deserialized = MemoryMarshal.Read<NetworkActionHeader>(buffer.ToArray());
#endif

        deserialized.ComponentId.Value.Should().Be(42);
        deserialized.TargetEntityId.Should().Be(123);
        deserialized.ExecuteTick.Should().Be(9999L);
        deserialized.DataLength.Should().Be(256);
    }

    [Fact]
    public void NetworkActionHeader_ShouldHandleZeroValues()
    {
        var header = new NetworkActionHeader
        {
            ComponentId = (DenseComponentId)0,
            TargetEntityId = 0,
            ExecuteTick = 0L,
            DataLength = 0
        };

        Span<byte> buffer = stackalloc byte[Marshal.SizeOf<NetworkActionHeader>()];
#if NET8_0_OR_GREATER
        MemoryMarshal.Write(buffer, in header);
#else
        MemoryMarshal.Write(buffer, ref header);
#endif

#if NET8_0_OR_GREATER
        var deserialized = MemoryMarshal.Read<NetworkActionHeader>(buffer);
#else
        var deserialized = MemoryMarshal.Read<NetworkActionHeader>(buffer.ToArray());
#endif

        deserialized.ComponentId.Value.Should().Be(0);
        deserialized.TargetEntityId.Should().Be(0);
        deserialized.ExecuteTick.Should().Be(0L);
        deserialized.DataLength.Should().Be(0);
    }

    [Fact]
    public void NetworkActionHeader_ShouldHandleNegativeValues()
    {
        var header = new NetworkActionHeader
        {
            ComponentId = (DenseComponentId)(-1),
            TargetEntityId = -999,
            ExecuteTick = -12345L,
            DataLength = -1
        };

        Span<byte> buffer = stackalloc byte[Marshal.SizeOf<NetworkActionHeader>()];
#if NET8_0_OR_GREATER
        MemoryMarshal.Write(buffer, in header);
#else
        MemoryMarshal.Write(buffer, ref header);
#endif

#if NET8_0_OR_GREATER
        var deserialized = MemoryMarshal.Read<NetworkActionHeader>(buffer);
#else
        var deserialized = MemoryMarshal.Read<NetworkActionHeader>(buffer.ToArray());
#endif

        deserialized.ComponentId.Value.Should().Be(-1);
        deserialized.TargetEntityId.Should().Be(-999);
        deserialized.ExecuteTick.Should().Be(-12345L);
        deserialized.DataLength.Should().Be(-1);
    }

    [Fact]
    public void NetworkActionHeader_ShouldHandleMaxValues()
    {
        var header = new NetworkActionHeader
        {
            ComponentId = (DenseComponentId)int.MaxValue,
            TargetEntityId = int.MaxValue,
            ExecuteTick = long.MaxValue,
            DataLength = int.MaxValue
        };

        Span<byte> buffer = stackalloc byte[Marshal.SizeOf<NetworkActionHeader>()];
#if NET8_0_OR_GREATER
        MemoryMarshal.Write(buffer, in header);
#else
        MemoryMarshal.Write(buffer, ref header);
#endif

#if NET8_0_OR_GREATER
        var deserialized = MemoryMarshal.Read<NetworkActionHeader>(buffer);
#else
        var deserialized = MemoryMarshal.Read<NetworkActionHeader>(buffer.ToArray());
#endif

        deserialized.ComponentId.Value.Should().Be(int.MaxValue);
        deserialized.TargetEntityId.Should().Be(int.MaxValue);
        deserialized.ExecuteTick.Should().Be(long.MaxValue);
        deserialized.DataLength.Should().Be(int.MaxValue);
    }

    [Fact]
    public void NetworkActionHeader_MultipleHeaders_ShouldSerializeSequentially()
    {
        var headers = new[]
        {
            new NetworkActionHeader { ComponentId = (DenseComponentId)1, TargetEntityId = 10, ExecuteTick = 100L, DataLength = 5 },
            new NetworkActionHeader { ComponentId = (DenseComponentId)2, TargetEntityId = 20, ExecuteTick = 200L, DataLength = 10 },
            new NetworkActionHeader { ComponentId = (DenseComponentId)3, TargetEntityId = 30, ExecuteTick = 300L, DataLength = 15 }
        };

        int headerSize = Marshal.SizeOf<NetworkActionHeader>();
        Span<byte> buffer = stackalloc byte[headerSize * 3];

        for (int i = 0; i < headers.Length; i++)
        {
#if NET8_0_OR_GREATER
            MemoryMarshal.Write(buffer.Slice(i * headerSize), in headers[i]);
#else
            var header = headers[i];
            MemoryMarshal.Write(buffer.Slice(i * headerSize), ref header);
#endif
        }

        for (int i = 0; i < headers.Length; i++)
        {
#if NET8_0_OR_GREATER
            var deserialized = MemoryMarshal.Read<NetworkActionHeader>(buffer.Slice(i * headerSize));
#else
            var deserialized = MemoryMarshal.Read<NetworkActionHeader>(buffer.Slice(i * headerSize).ToArray());
#endif
            deserialized.ComponentId.Value.Should().Be(headers[i].ComponentId);
            deserialized.TargetEntityId.Should().Be(headers[i].TargetEntityId);
            deserialized.ExecuteTick.Should().Be(headers[i].ExecuteTick);
            deserialized.DataLength.Should().Be(headers[i].DataLength);
        }
    }
}
