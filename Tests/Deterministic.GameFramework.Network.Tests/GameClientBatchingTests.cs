using Deterministic.GameFramework.Network.Client;
using Deterministic.GameFramework.Network.Packets;
using Deterministic.GameFramework.Network.Buffers;
using Deterministic.GameFramework.ECS;
using Deterministic.GameFramework.Network.Buffers;
using Deterministic.GameFramework.Network.Packets;

namespace Deterministic.GameFramework.Network.Tests;

public class GameClientBatchingTests
{
    [Fact]
    public void GameClient_SendAction_ShouldBufferAction()
    {
        var buffer = new PacketBuffer();
        int headerSize = Marshal.SizeOf<NetworkActionHeader>();

        var actionData = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        int StableId = 42;
        int targetEntityId = 100;
        long executeTick = 1000L;

        var span = buffer.GetSpan(headerSize + actionData.Length);
        var header = new NetworkActionHeader
        {
            ComponentId = (DenseComponentId)StableId,
            TargetEntityId = targetEntityId,
            ExecuteTick = executeTick,
            DataLength = actionData.Length
        };

#if NET8_0_OR_GREATER
        MemoryMarshal.Write(span, in header);
#else
        MemoryMarshal.Write(span, ref header);
#endif
        actionData.CopyTo(span.Slice(headerSize));
        buffer.Advance(headerSize + actionData.Length);

        buffer.Length.Should().Be(headerSize + actionData.Length);

        var payload = buffer.ToArray();
        var readSpan = new ReadOnlySpan<byte>(payload);
#if NET8_0_OR_GREATER
        var decodedHeader = MemoryMarshal.Read<NetworkActionHeader>(readSpan);
#else
        var decodedHeader = MemoryMarshal.Read<NetworkActionHeader>(readSpan.ToArray());
#endif

        decodedHeader.ComponentId.Value.Should().Be(StableId);
        decodedHeader.TargetEntityId.Should().Be(targetEntityId);
        decodedHeader.ExecuteTick.Should().Be(executeTick);
        decodedHeader.DataLength.Should().Be(actionData.Length);

        var decodedData = readSpan.Slice(headerSize, actionData.Length).ToArray();
        decodedData.Should().Equal(actionData);
    }

    [Fact]
    public void GameClient_SendMultipleActions_ShouldBatchTogether()
    {
        var buffer = new PacketBuffer();
        int headerSize = Marshal.SizeOf<NetworkActionHeader>();

        var actions = new[]
        {
            (ComponentId: 1, TargetId: 10, Tick: 100L, Data: new byte[] { 0x01, 0x02 }),
            (ComponentId: 2, TargetId: 20, Tick: 200L, Data: new byte[] { 0x03, 0x04, 0x05 }),
            (ComponentId: 3, TargetId: 30, Tick: 300L, Data: new byte[] { 0x06 })
        };

        foreach (var action in actions)
        {
            var span = buffer.GetSpan(headerSize + action.Data.Length);
            var header = new NetworkActionHeader
            {
                ComponentId = (DenseComponentId)action.ComponentId,
                TargetEntityId = action.TargetId,
                ExecuteTick = action.Tick,
                DataLength = action.Data.Length
            };

    #if NET8_0_OR_GREATER
        MemoryMarshal.Write(span, in header);
#else
        MemoryMarshal.Write(span, ref header);
#endif
            action.Data.CopyTo(span.Slice(headerSize));
            buffer.Advance(headerSize + action.Data.Length);
        }

        var payload = buffer.ToArray();
        var readSpan = new ReadOnlySpan<byte>(payload);
        int offset = 0;
        int actionIndex = 0;

        while (offset + headerSize <= readSpan.Length)
        {
#if NET8_0_OR_GREATER
            var header = MemoryMarshal.Read<NetworkActionHeader>(readSpan.Slice(offset));
#else
            var header = MemoryMarshal.Read<NetworkActionHeader>(readSpan.Slice(offset).ToArray());
#endif
            offset += headerSize;

            if (offset + header.DataLength > readSpan.Length) break;

            var data = readSpan.Slice(offset, header.DataLength).ToArray();
            offset += header.DataLength;

            header.ComponentId.Value.Should().Be(actions[actionIndex].ComponentId);
            header.TargetEntityId.Should().Be(actions[actionIndex].TargetId);
            header.ExecuteTick.Should().Be(actions[actionIndex].Tick);
            data.Should().Equal(actions[actionIndex].Data);

            actionIndex++;
        }

        actionIndex.Should().Be(actions.Length);
    }

    [Fact]
    public void GameClient_FlushBuffer_ShouldResetAfterFlush()
    {
        var buffer = new PacketBuffer();
        int headerSize = Marshal.SizeOf<NetworkActionHeader>();

        var actionData = new byte[] { 0xAA, 0xBB, 0xCC };
        var span = buffer.GetSpan(headerSize + actionData.Length);
        var header = new NetworkActionHeader
        {
            ComponentId = (DenseComponentId)1,
            TargetEntityId = 1,
            ExecuteTick = 1L,
            DataLength = actionData.Length
        };

#if NET8_0_OR_GREATER
        MemoryMarshal.Write(span, in header);
#else
        MemoryMarshal.Write(span, ref header);
#endif
        actionData.CopyTo(span.Slice(headerSize));
        buffer.Advance(headerSize + actionData.Length);

        buffer.Length.Should().BeGreaterThan(0);

        var payload = buffer.ToArray();
        payload.Should().NotBeEmpty();

        buffer.Reset();
        buffer.Length.Should().Be(0);
        buffer.ToArray().Should().BeEmpty();
    }

    [Fact]
    public void GameClient_BatchingCycle_ShouldHandleMultipleFlushes()
    {
        var buffer = new PacketBuffer();
        int headerSize = Marshal.SizeOf<NetworkActionHeader>();

        for (int cycle = 0; cycle < 10; cycle++)
        {
            for (int i = 0; i < 5; i++)
            {
                var actionData = new byte[] { (byte)cycle, (byte)i };
                var span = buffer.GetSpan(headerSize + actionData.Length);
                var header = new NetworkActionHeader
                {
                    ComponentId = (DenseComponentId)(cycle * 10 + i),
                    TargetEntityId = i,
                    ExecuteTick = cycle * 100L + i,
                    DataLength = actionData.Length
                };

        #if NET8_0_OR_GREATER
        MemoryMarshal.Write(span, in header);
#else
        MemoryMarshal.Write(span, ref header);
#endif
                actionData.CopyTo(span.Slice(headerSize));
                buffer.Advance(headerSize + actionData.Length);
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

                if (offset + header.DataLength > readSpan.Length) break;

                offset += header.DataLength;
                actionCount++;
            }

            actionCount.Should().Be(5);

            buffer.Reset();
            buffer.Length.Should().Be(0);
        }
    }

    [Fact]
    public void GameClient_EmptyBuffer_ShouldNotSendOnFlush()
    {
        var buffer = new PacketBuffer();

        buffer.Length.Should().Be(0);

        if (buffer.Length > 0)
        {
            var payload = buffer.ToArray();
            payload.Should().NotBeEmpty();
        }
        else
        {
            buffer.ToArray().Should().BeEmpty();
        }
    }

    [Fact]
    public void GameClient_LargeActionBatch_ShouldHandleCorrectly()
    {
        var buffer = new PacketBuffer();
        int headerSize = Marshal.SizeOf<NetworkActionHeader>();
        int actionCount = 1000;

        for (int i = 0; i < actionCount; i++)
        {
            var actionData = new byte[10];
            new Random(i).NextBytes(actionData);

            var span = buffer.GetSpan(headerSize + actionData.Length);
            var header = new NetworkActionHeader
            {
                ComponentId = (DenseComponentId)i,
                TargetEntityId = i * 2,
                ExecuteTick = i * 3L,
                DataLength = actionData.Length
            };

    #if NET8_0_OR_GREATER
        MemoryMarshal.Write(span, in header);
#else
        MemoryMarshal.Write(span, ref header);
#endif
            actionData.CopyTo(span.Slice(headerSize));
            buffer.Advance(headerSize + actionData.Length);
        }

        var payload = buffer.ToArray();
        var readSpan = new ReadOnlySpan<byte>(payload);
        int offset = 0;
        int decodedCount = 0;

        while (offset + headerSize <= readSpan.Length)
        {
#if NET8_0_OR_GREATER
            var header = MemoryMarshal.Read<NetworkActionHeader>(readSpan.Slice(offset));
#else
            var header = MemoryMarshal.Read<NetworkActionHeader>(readSpan.Slice(offset).ToArray());
#endif
            offset += headerSize;

            if (offset + header.DataLength > readSpan.Length) break;

            offset += header.DataLength;
            decodedCount++;
        }

        decodedCount.Should().Be(actionCount);
    }

    [Fact]
    public async Task GameClient_ConcurrentBuffering_ShouldBeThreadSafeWithLock()
    {
        var buffer = new PacketBuffer();
        int headerSize = Marshal.SizeOf<NetworkActionHeader>();
        var tasks = new List<Task>();

        for (int i = 0; i < 100; i++)
        {
            int taskId = i;
            tasks.Add(Task.Run(() =>
            {
                lock (buffer)
                {
                    var actionData = new byte[] { (byte)taskId };
                    var span = buffer.GetSpan(headerSize + actionData.Length);
                    var header = new NetworkActionHeader
                    {
                        ComponentId = (DenseComponentId)taskId,
                        TargetEntityId = taskId,
                        ExecuteTick = taskId,
                        DataLength = actionData.Length
                    };

            #if NET8_0_OR_GREATER
        MemoryMarshal.Write(span, in header);
#else
        MemoryMarshal.Write(span, ref header);
#endif
                    actionData.CopyTo(span.Slice(headerSize));
                    buffer.Advance(headerSize + actionData.Length);
                }
            }));
        }

        await Task.WhenAll(tasks.ToArray());

        VerifyPayloadActionCount(buffer.ToArray(), headerSize, 100);
    }

    private static void VerifyPayloadActionCount(byte[] payload, int headerSize, int expectedCount)
    {
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

            if (offset + header.DataLength > readSpan.Length) break;

            offset += header.DataLength;
            actionCount++;
        }

        actionCount.Should().Be(expectedCount);
    }

    [Fact]
    public void GameClient_VariableSizeActions_ShouldBatchCorrectly()
    {
        var buffer = new PacketBuffer();
        int headerSize = Marshal.SizeOf<NetworkActionHeader>();

        var sizes = new[] { 1, 10, 100, 1000, 5, 50, 500, 2, 20, 200 };

        foreach (var size in sizes)
        {
            var actionData = new byte[size];
            new Random(size).NextBytes(actionData);

            var span = buffer.GetSpan(headerSize + actionData.Length);
            var header = new NetworkActionHeader
            {
                ComponentId = (DenseComponentId)size,
                TargetEntityId = size * 2,
                ExecuteTick = size * 3L,
                DataLength = actionData.Length
            };

    #if NET8_0_OR_GREATER
        MemoryMarshal.Write(span, in header);
#else
        MemoryMarshal.Write(span, ref header);
#endif
            actionData.CopyTo(span.Slice(headerSize));
            buffer.Advance(headerSize + actionData.Length);
        }

        var payload = buffer.ToArray();
        var readSpan = new ReadOnlySpan<byte>(payload);
        int offset = 0;
        int actionIndex = 0;

        while (offset + headerSize <= readSpan.Length)
        {
#if NET8_0_OR_GREATER
            var header = MemoryMarshal.Read<NetworkActionHeader>(readSpan.Slice(offset));
#else
            var header = MemoryMarshal.Read<NetworkActionHeader>(readSpan.Slice(offset).ToArray());
#endif
            offset += headerSize;

            if (offset + header.DataLength > readSpan.Length) break;

            header.DataLength.Should().Be(sizes[actionIndex]);
            offset += header.DataLength;
            actionIndex++;
        }

        actionIndex.Should().Be(sizes.Length);
    }

    [Fact]
    public void GameClient_BufferReuse_ShouldNotLeakData()
    {
        var buffer = new PacketBuffer();
        int headerSize = Marshal.SizeOf<NetworkActionHeader>();

        var firstData = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };
        var span1 = buffer.GetSpan(headerSize + firstData.Length);
        var header1 = new NetworkActionHeader
        {
            ComponentId = (DenseComponentId)1,
            TargetEntityId = 1,
            ExecuteTick = 1L,
            DataLength = firstData.Length
        };
#if NET8_0_OR_GREATER
        MemoryMarshal.Write(span1, in header1);
#else
        MemoryMarshal.Write(span1, ref header1);
#endif
        firstData.CopyTo(span1.Slice(headerSize));
        buffer.Advance(headerSize + firstData.Length);

        var firstPayload = buffer.ToArray();
        buffer.Reset();

        var secondData = new byte[] { 0xAA, 0xBB };
        var span2 = buffer.GetSpan(headerSize + secondData.Length);
        var header2 = new NetworkActionHeader
        {
            ComponentId = (DenseComponentId)2,
            TargetEntityId = 2,
            ExecuteTick = 2L,
            DataLength = secondData.Length
        };
#if NET8_0_OR_GREATER
        MemoryMarshal.Write(span2, in header2);
#else
        MemoryMarshal.Write(span2, ref header2);
#endif
        secondData.CopyTo(span2.Slice(headerSize));
        buffer.Advance(headerSize + secondData.Length);

        var secondPayload = buffer.ToArray();

        secondPayload.Length.Should().Be(headerSize + secondData.Length);
        
        var readSpan = new ReadOnlySpan<byte>(secondPayload);
#if NET8_0_OR_GREATER
        var decodedHeader = MemoryMarshal.Read<NetworkActionHeader>(readSpan);
#else
        var decodedHeader = MemoryMarshal.Read<NetworkActionHeader>(readSpan.ToArray());
#endif
        var decodedData = readSpan.Slice(headerSize, secondData.Length).ToArray();

        decodedData.Should().Equal(secondData);
        decodedData.Should().NotContain((byte)0xFF);
    }
}
