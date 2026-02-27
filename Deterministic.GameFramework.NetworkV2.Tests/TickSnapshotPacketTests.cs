using Deterministic.GameFramework.NetworkV2.Packets;
using Deterministic.GameFramework.NetworkV2.Buffers;

namespace Deterministic.GameFramework.NetworkV2.Tests;

public class TickSnapshotPacketTests
{
    [Fact]
    public void TickSnapshotPacket_ShouldEncodeAndDecodeEmptyPayload()
    {
        var packet = new TickSnapshotPacket
        {
            ServerTick = 12345L,
            Payload = Array.Empty<byte>()
        };

        packet.ServerTick.Should().Be(12345L);
        packet.Payload.Should().BeEmpty();
    }

    [Fact]
    public void TickSnapshotPacket_ShouldEncodeAndDecodeSingleAction()
    {
        var buffer = new PacketBuffer();
        int headerSize = Marshal.SizeOf<NetworkActionHeader>();

        var header = new NetworkActionHeader
        {
            NetworkId = 42,
            TargetEntityId = 100,
            ExecuteTick = 1000L,
            DataLength = 4
        };

        var actionData = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD };

        var span = buffer.GetSpan(headerSize + actionData.Length);
        MemoryMarshal.Write(span, in header);
        actionData.CopyTo(span.Slice(headerSize));
        buffer.Advance(headerSize + actionData.Length);

        var packet = new TickSnapshotPacket
        {
            ServerTick = 5000L,
            Payload = buffer.ToArray()
        };

        var readSpan = new ReadOnlySpan<byte>(packet.Payload);
        var decodedHeader = MemoryMarshal.Read<NetworkActionHeader>(readSpan);

        decodedHeader.NetworkId.Should().Be(42);
        decodedHeader.TargetEntityId.Should().Be(100);
        decodedHeader.ExecuteTick.Should().Be(1000L);
        decodedHeader.DataLength.Should().Be(4);

        var decodedData = readSpan.Slice(headerSize, 4).ToArray();
        decodedData.Should().Equal(actionData);
    }

    [Fact]
    public void TickSnapshotPacket_ShouldEncodeAndDecodeMultipleActions()
    {
        var buffer = new PacketBuffer();
        int headerSize = Marshal.SizeOf<NetworkActionHeader>();

        var actions = new[]
        {
            (NetworkId: 1, TargetId: 10, Tick: 100L, Data: new byte[] { 0x01, 0x02 }),
            (NetworkId: 2, TargetId: 20, Tick: 200L, Data: new byte[] { 0x03, 0x04, 0x05 }),
            (NetworkId: 3, TargetId: 30, Tick: 300L, Data: new byte[] { 0x06 }),
            (NetworkId: 4, TargetId: 40, Tick: 400L, Data: new byte[] { 0x07, 0x08, 0x09, 0x0A })
        };

        foreach (var action in actions)
        {
            var header = new NetworkActionHeader
            {
                NetworkId = action.NetworkId,
                TargetEntityId = action.TargetId,
                ExecuteTick = action.Tick,
                DataLength = action.Data.Length
            };

            var span = buffer.GetSpan(headerSize + action.Data.Length);
            MemoryMarshal.Write(span, in header);
            action.Data.CopyTo(span.Slice(headerSize));
            buffer.Advance(headerSize + action.Data.Length);
        }

        var packet = new TickSnapshotPacket
        {
            ServerTick = 9999L,
            Payload = buffer.ToArray()
        };

        var readSpan = new ReadOnlySpan<byte>(packet.Payload);
        int offset = 0;
        int actionIndex = 0;

        while (offset + headerSize <= readSpan.Length)
        {
            var header = MemoryMarshal.Read<NetworkActionHeader>(readSpan.Slice(offset));
            offset += headerSize;

            if (offset + header.DataLength > readSpan.Length) break;

            var data = readSpan.Slice(offset, header.DataLength).ToArray();
            offset += header.DataLength;

            header.NetworkId.Should().Be(actions[actionIndex].NetworkId);
            header.TargetEntityId.Should().Be(actions[actionIndex].TargetId);
            header.ExecuteTick.Should().Be(actions[actionIndex].Tick);
            header.DataLength.Should().Be(actions[actionIndex].Data.Length);
            data.Should().Equal(actions[actionIndex].Data);

            actionIndex++;
        }

        actionIndex.Should().Be(actions.Length);
    }

    [Fact]
    public void TickSnapshotPacket_ShouldHandleLargePayload()
    {
        var buffer = new PacketBuffer();
        int headerSize = Marshal.SizeOf<NetworkActionHeader>();

        for (int i = 0; i < 100; i++)
        {
            var header = new NetworkActionHeader
            {
                NetworkId = i,
                TargetEntityId = i * 10,
                ExecuteTick = i * 100L,
                DataLength = 10
            };

            var data = new byte[10];
            new Random(i).NextBytes(data);

            var span = buffer.GetSpan(headerSize + data.Length);
            MemoryMarshal.Write(span, in header);
            data.CopyTo(span.Slice(headerSize));
            buffer.Advance(headerSize + data.Length);
        }

        var packet = new TickSnapshotPacket
        {
            ServerTick = 50000L,
            Payload = buffer.ToArray()
        };

        var readSpan = new ReadOnlySpan<byte>(packet.Payload);
        int offset = 0;
        int count = 0;

        while (offset + headerSize <= readSpan.Length)
        {
            var header = MemoryMarshal.Read<NetworkActionHeader>(readSpan.Slice(offset));
            offset += headerSize;

            if (offset + header.DataLength > readSpan.Length) break;

            offset += header.DataLength;
            count++;
        }

        count.Should().Be(100);
    }

    [Fact]
    public void TickSnapshotPacket_ShouldHandleZeroLengthActionData()
    {
        var buffer = new PacketBuffer();
        int headerSize = Marshal.SizeOf<NetworkActionHeader>();

        var header = new NetworkActionHeader
        {
            NetworkId = 99,
            TargetEntityId = 999,
            ExecuteTick = 9999L,
            DataLength = 0
        };

        var span = buffer.GetSpan(headerSize);
        MemoryMarshal.Write(span, in header);
        buffer.Advance(headerSize);

        var packet = new TickSnapshotPacket
        {
            ServerTick = 1L,
            Payload = buffer.ToArray()
        };

        var readSpan = new ReadOnlySpan<byte>(packet.Payload);
        var decodedHeader = MemoryMarshal.Read<NetworkActionHeader>(readSpan);

        decodedHeader.NetworkId.Should().Be(99);
        decodedHeader.TargetEntityId.Should().Be(999);
        decodedHeader.ExecuteTick.Should().Be(9999L);
        decodedHeader.DataLength.Should().Be(0);
    }

    [Fact]
    public void TickSnapshotPacket_ShouldHandleMixedActionSizes()
    {
        var buffer = new PacketBuffer();
        int headerSize = Marshal.SizeOf<NetworkActionHeader>();

        var dataSizes = new[] { 0, 1, 10, 100, 1000, 5, 50, 500 };

        foreach (var size in dataSizes)
        {
            var header = new NetworkActionHeader
            {
                NetworkId = size,
                TargetEntityId = size * 2,
                ExecuteTick = size * 3L,
                DataLength = size
            };

            var data = new byte[size];
            if (size > 0)
            {
                new Random(size).NextBytes(data);
            }

            var span = buffer.GetSpan(headerSize + size);
            MemoryMarshal.Write(span, in header);
            if (size > 0)
            {
                data.CopyTo(span.Slice(headerSize));
            }
            buffer.Advance(headerSize + size);
        }

        var packet = new TickSnapshotPacket
        {
            ServerTick = 777L,
            Payload = buffer.ToArray()
        };

        var readSpan = new ReadOnlySpan<byte>(packet.Payload);
        int offset = 0;
        int actionIndex = 0;

        while (offset + headerSize <= readSpan.Length)
        {
            var header = MemoryMarshal.Read<NetworkActionHeader>(readSpan.Slice(offset));
            offset += headerSize;

            if (offset + header.DataLength > readSpan.Length) break;

            header.DataLength.Should().Be(dataSizes[actionIndex]);
            offset += header.DataLength;
            actionIndex++;
        }

        actionIndex.Should().Be(dataSizes.Length);
    }
}
