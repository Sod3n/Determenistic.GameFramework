using Deterministic.GameFramework.Network.Packets;
using Deterministic.GameFramework.Network.Buffers;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Network.Buffers;
using Deterministic.GameFramework.Network.Packets;

namespace Deterministic.GameFramework.Network.Tests;

public class MalformedPacketTests
{
    [Fact]
    public void MalformedPacket_ShouldHandleIncompleteHeader()
    {
        var incompleteHeader = new byte[10];
        int headerSize = Marshal.SizeOf<NetworkActionHeader>();

        var span = new ReadOnlySpan<byte>(incompleteHeader);
        int offset = 0;
        int actionCount = 0;

        while (offset + headerSize <= span.Length)
        {
#if NET8_0_OR_GREATER
            var header = MemoryMarshal.Read<NetworkActionHeader>(span.Slice(offset));
#else
            var header = MemoryMarshal.Read<NetworkActionHeader>(span.Slice(offset).ToArray());
#endif
            offset += headerSize;

            if (offset + header.DataLength > span.Length) break;

            offset += header.DataLength;
            actionCount++;
        }

        actionCount.Should().Be(0);
    }

    [Fact]
    public void MalformedPacket_ShouldHandleTruncatedData()
    {
        var buffer = new PacketBuffer();
        int headerSize = Marshal.SizeOf<NetworkActionHeader>();

        var header = new NetworkActionHeader
        {
            ComponentId = (DenseComponentId)1,
            TargetEntityId = 10,
            ExecuteTick = 100L,
            DataLength = 100
        };

        var span = buffer.GetSpan(headerSize);
#if NET8_0_OR_GREATER
        MemoryMarshal.Write(span, in header);
#else
        MemoryMarshal.Write(span, ref header);
#endif
        buffer.Advance(headerSize);

        var truncatedData = new byte[50];
        var dataSpan = buffer.GetSpan(truncatedData.Length);
        truncatedData.CopyTo(dataSpan);
        buffer.Advance(truncatedData.Length);

        var payload = buffer.ToArray();
        var readSpan = new ReadOnlySpan<byte>(payload);
        int offset = 0;
        int actionCount = 0;

        while (offset + headerSize <= readSpan.Length)
        {
#if NET8_0_OR_GREATER
            var decodedHeader = MemoryMarshal.Read<NetworkActionHeader>(readSpan.Slice(offset));
#else
            var decodedHeader = MemoryMarshal.Read<NetworkActionHeader>(readSpan.Slice(offset).ToArray());
#endif
            offset += headerSize;

            if (offset + decodedHeader.DataLength > readSpan.Length)
            {
                break;
            }

            offset += decodedHeader.DataLength;
            actionCount++;
        }

        actionCount.Should().Be(0);
    }

    [Fact]
    public void MalformedPacket_ShouldHandleNegativeDataLength()
    {
        var buffer = new PacketBuffer();
        int headerSize = Marshal.SizeOf<NetworkActionHeader>();

        var header = new NetworkActionHeader
        {
            ComponentId = (DenseComponentId)1,
            TargetEntityId = 10,
            ExecuteTick = 100L,
            DataLength = -50
        };

        var span = buffer.GetSpan(headerSize);
#if NET8_0_OR_GREATER
        MemoryMarshal.Write(span, in header);
#else
        MemoryMarshal.Write(span, ref header);
#endif
        buffer.Advance(headerSize);

        var payload = buffer.ToArray();
        var readSpan = new ReadOnlySpan<byte>(payload);
        int offset = 0;
        int actionCount = 0;

        while (offset + headerSize <= readSpan.Length)
        {
#if NET8_0_OR_GREATER
            var decodedHeader = MemoryMarshal.Read<NetworkActionHeader>(readSpan.Slice(offset));
#else
            var decodedHeader = MemoryMarshal.Read<NetworkActionHeader>(readSpan.Slice(offset).ToArray());
#endif
            offset += headerSize;

            if (offset + decodedHeader.DataLength > readSpan.Length || decodedHeader.DataLength < 0)
            {
                break;
            }

            offset += decodedHeader.DataLength;
            actionCount++;
        }

        actionCount.Should().Be(0);
    }

    [Fact]
    public void MalformedPacket_ShouldHandleExcessiveDataLength()
    {
        var buffer = new PacketBuffer();
        int headerSize = Marshal.SizeOf<NetworkActionHeader>();

        var header = new NetworkActionHeader
        {
            ComponentId = (DenseComponentId)1,
            TargetEntityId = 10,
            ExecuteTick = 100L,
            DataLength = int.MaxValue
        };

        var span = buffer.GetSpan(headerSize);
#if NET8_0_OR_GREATER
        MemoryMarshal.Write(span, in header);
#else
        MemoryMarshal.Write(span, ref header);
#endif
        buffer.Advance(headerSize);

        var payload = buffer.ToArray();
        var readSpan = new ReadOnlySpan<byte>(payload);
        int offset = 0;
        int actionCount = 0;

        while (offset + headerSize <= readSpan.Length)
        {
#if NET8_0_OR_GREATER
            var decodedHeader = MemoryMarshal.Read<NetworkActionHeader>(readSpan.Slice(offset));
#else
            var decodedHeader = MemoryMarshal.Read<NetworkActionHeader>(readSpan.Slice(offset).ToArray());
#endif
            offset += headerSize;

            if (decodedHeader.DataLength < 0 || (long)offset + decodedHeader.DataLength > readSpan.Length)
            {
                break;
            }

            offset += decodedHeader.DataLength;
            actionCount++;
        }

        actionCount.Should().Be(0);
    }

    [Fact]
    public void MalformedPacket_ShouldHandlePartiallyCorruptedMultiActionPayload()
    {
        var buffer = new PacketBuffer();
        int headerSize = Marshal.SizeOf<NetworkActionHeader>();

        var header1 = new NetworkActionHeader
        {
            ComponentId = (DenseComponentId)1,
            TargetEntityId = 10,
            ExecuteTick = 100L,
            DataLength = 5
        };
        var data1 = new byte[] { 1, 2, 3, 4, 5 };

        var span1 = buffer.GetSpan(headerSize + data1.Length);
#if NET8_0_OR_GREATER
        MemoryMarshal.Write(span1, in header1);
#else
        MemoryMarshal.Write(span1, ref header1);
#endif
        data1.CopyTo(span1.Slice(headerSize));
        buffer.Advance(headerSize + data1.Length);

        var header2 = new NetworkActionHeader
        {
            ComponentId = (DenseComponentId)2,
            TargetEntityId = 20,
            ExecuteTick = 200L,
            DataLength = 1000
        };

        var span2 = buffer.GetSpan(headerSize);
#if NET8_0_OR_GREATER
        MemoryMarshal.Write(span2, in header2);
#else
        MemoryMarshal.Write(span2, ref header2);
#endif
        buffer.Advance(headerSize);

        var payload = buffer.ToArray();
        var readSpan = new ReadOnlySpan<byte>(payload);
        int offset = 0;
        int actionCount = 0;

        while (offset + headerSize <= readSpan.Length)
        {
#if NET8_0_OR_GREATER
            var decodedHeader = MemoryMarshal.Read<NetworkActionHeader>(readSpan.Slice(offset));
#else
            var decodedHeader = MemoryMarshal.Read<NetworkActionHeader>(readSpan.Slice(offset).ToArray());
#endif
            offset += headerSize;

            if (offset + decodedHeader.DataLength > readSpan.Length)
            {
                break;
            }

            offset += decodedHeader.DataLength;
            actionCount++;
        }

        actionCount.Should().Be(1);
    }

    [Fact]
    public void MalformedPacket_ShouldHandleEmptyPayload()
    {
        var payload = Array.Empty<byte>();
        var readSpan = new ReadOnlySpan<byte>(payload);
        int headerSize = Marshal.SizeOf<NetworkActionHeader>();
        int offset = 0;
        int actionCount = 0;

        while (offset + headerSize <= readSpan.Length)
        {
#if NET8_0_OR_GREATER
            var header = MemoryMarshal.Read<NetworkActionHeader>(readSpan.Slice(offset));
#else
            var header = MemoryMarshal.Read<NetworkActionHeader>(readSpan.Slice(offset).ToArray());
#endif
            offset += headerSize;

            if (offset + header.DataLength > readSpan.Length)
            {
                break;
            }

            offset += header.DataLength;
            actionCount++;
        }

        actionCount.Should().Be(0);
    }

    [Fact]
    public void MalformedPacket_ShouldHandleRandomGarbage()
    {
        var random = new Random(42);
        var garbage = new byte[256];
        random.NextBytes(garbage);

        var readSpan = new ReadOnlySpan<byte>(garbage);
        int headerSize = Marshal.SizeOf<NetworkActionHeader>();
        int offset = 0;
        int validActionCount = 0;

        while (offset + headerSize <= readSpan.Length)
        {
#if NET8_0_OR_GREATER
            var header = MemoryMarshal.Read<NetworkActionHeader>(readSpan.Slice(offset));
#else
            var header = MemoryMarshal.Read<NetworkActionHeader>(readSpan.Slice(offset).ToArray());
#endif
            offset += headerSize;

            if (header.DataLength < 0 || offset + header.DataLength > readSpan.Length)
            {
                break;
            }

            offset += header.DataLength;
            validActionCount++;
        }

        validActionCount.Should().BeLessThan(10);
    }

    [Fact]
    public void MalformedPacket_ShouldStopAtFirstInvalidAction()
    {
        var buffer = new PacketBuffer();
        int headerSize = Marshal.SizeOf<NetworkActionHeader>();

        for (int i = 0; i < 5; i++)
        {
            var header = new NetworkActionHeader
            {
                ComponentId = (DenseComponentId)i,
                TargetEntityId = i * 10,
                ExecuteTick = i * 100L,
                DataLength = 4
            };
            var data = new byte[] { (byte)i, (byte)i, (byte)i, (byte)i };

            var span = buffer.GetSpan(headerSize + data.Length);
    #if NET8_0_OR_GREATER
        MemoryMarshal.Write(span, in header);
#else
        MemoryMarshal.Write(span, ref header);
#endif
            data.CopyTo(span.Slice(headerSize));
            buffer.Advance(headerSize + data.Length);
        }

        var corruptHeader = new NetworkActionHeader
        {
            ComponentId = (DenseComponentId)999,
            TargetEntityId = 9999,
            ExecuteTick = 99999L,
            DataLength = 10000
        };
        var corruptSpan = buffer.GetSpan(headerSize);
#if NET8_0_OR_GREATER
        MemoryMarshal.Write(corruptSpan, in corruptHeader);
#else
        MemoryMarshal.Write(corruptSpan, ref corruptHeader);
#endif
        buffer.Advance(headerSize);

        var payload = buffer.ToArray();
        var readSpan = new ReadOnlySpan<byte>(payload);
        int offset = 0;
        int actionCount = 0;

        while (offset + headerSize <= readSpan.Length)
        {
#if NET8_0_OR_GREATER
            var header = MemoryMarshal.Read<NetworkActionHeader>(readSpan.Slice(offset));
#else
            var header = MemoryMarshal.Read<NetworkActionHeader>(readSpan.Slice(offset).ToArray());
#endif
            offset += headerSize;

            if (offset + header.DataLength > readSpan.Length)
            {
                break;
            }

            offset += header.DataLength;
            actionCount++;
        }

        actionCount.Should().Be(5);
    }

    [Fact]
    public void MalformedPacket_ShouldHandleZeroDataLengthInMiddle()
    {
        var buffer = new PacketBuffer();
        int headerSize = Marshal.SizeOf<NetworkActionHeader>();

        var actions = new[]
        {
            (DataLength: 5, Data: new byte[] { 1, 2, 3, 4, 5 }),
            (DataLength: 0, Data: Array.Empty<byte>()),
            (DataLength: 3, Data: new byte[] { 6, 7, 8 })
        };

        foreach (var action in actions)
        {
            var header = new NetworkActionHeader
            {
                ComponentId = (DenseComponentId)1,
                TargetEntityId = 1,
                ExecuteTick = 1L,
                DataLength = action.DataLength
            };

            var span = buffer.GetSpan(headerSize + action.DataLength);
    #if NET8_0_OR_GREATER
        MemoryMarshal.Write(span, in header);
#else
        MemoryMarshal.Write(span, ref header);
#endif
            if (action.DataLength > 0)
            {
                action.Data.CopyTo(span.Slice(headerSize));
            }
            buffer.Advance(headerSize + action.DataLength);
        }

        var payload = buffer.ToArray();
        var readSpan = new ReadOnlySpan<byte>(payload);
        int offset = 0;
        int actionCount = 0;

        while (offset + headerSize <= readSpan.Length)
        {
#if NET8_0_OR_GREATER
            var header = MemoryMarshal.Read<NetworkActionHeader>(readSpan.Slice(offset));
#else
            var header = MemoryMarshal.Read<NetworkActionHeader>(readSpan.Slice(offset).ToArray());
#endif
            offset += headerSize;

            if (offset + header.DataLength > readSpan.Length)
            {
                break;
            }

            offset += header.DataLength;
            actionCount++;
        }

        actionCount.Should().Be(3);
    }
}
