using Deterministic.GameFramework.Network.Buffers;
using Deterministic.GameFramework.Network.Buffers;

namespace Deterministic.GameFramework.Network.Tests;

public class PacketBufferTests
{
    [Fact]
    public void PacketBuffer_ShouldStartEmpty()
    {
        var buffer = new PacketBuffer();
        
        buffer.Length.Should().Be(0);
    }

    [Fact]
    public void PacketBuffer_ShouldWriteAndReadData()
    {
        var buffer = new PacketBuffer();
        var testData = new byte[] { 1, 2, 3, 4, 5 };

        var span = buffer.GetSpan(testData.Length);
        testData.CopyTo(span);
        buffer.Advance(testData.Length);

        buffer.Length.Should().Be(testData.Length);
        var result = buffer.ToArray();
        result.Should().Equal(testData);
    }

    [Fact]
    public void PacketBuffer_ShouldReset()
    {
        var buffer = new PacketBuffer();
        var testData = new byte[] { 1, 2, 3, 4, 5 };

        var span = buffer.GetSpan(testData.Length);
        testData.CopyTo(span);
        buffer.Advance(testData.Length);

        buffer.Reset();

        buffer.Length.Should().Be(0);
        buffer.ToArray().Should().BeEmpty();
    }

    [Fact]
    public void PacketBuffer_ShouldResizeWhenNeeded()
    {
        var buffer = new PacketBuffer();
        var largeData = new byte[8192];
        new Random(42).NextBytes(largeData);

        var span = buffer.GetSpan(largeData.Length);
        largeData.CopyTo(span);
        buffer.Advance(largeData.Length);

        buffer.Length.Should().Be(largeData.Length);
        var result = buffer.ToArray();
        result.Should().Equal(largeData);
    }

    [Fact]
    public void PacketBuffer_ShouldHandleMultipleWrites()
    {
        var buffer = new PacketBuffer();
        var data1 = new byte[] { 1, 2, 3 };
        var data2 = new byte[] { 4, 5, 6 };
        var data3 = new byte[] { 7, 8, 9 };

        var span1 = buffer.GetSpan(data1.Length);
        data1.CopyTo(span1);
        buffer.Advance(data1.Length);

        var span2 = buffer.GetSpan(data2.Length);
        data2.CopyTo(span2);
        buffer.Advance(data2.Length);

        var span3 = buffer.GetSpan(data3.Length);
        data3.CopyTo(span3);
        buffer.Advance(data3.Length);

        buffer.Length.Should().Be(9);
        var result = buffer.ToArray();
        result.Should().Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 });
    }

    [Fact]
    public void PacketBuffer_ShouldReuseAfterReset()
    {
        var buffer = new PacketBuffer();
        var data1 = new byte[] { 1, 2, 3, 4, 5 };
        var data2 = new byte[] { 6, 7, 8 };

        var span1 = buffer.GetSpan(data1.Length);
        data1.CopyTo(span1);
        buffer.Advance(data1.Length);
        buffer.ToArray().Should().Equal(data1);

        buffer.Reset();

        var span2 = buffer.GetSpan(data2.Length);
        data2.CopyTo(span2);
        buffer.Advance(data2.Length);
        buffer.ToArray().Should().Equal(data2);
    }

    [Fact]
    public void PacketBuffer_ShouldHandleZeroSizeRequest()
    {
        var buffer = new PacketBuffer();
        
        var span = buffer.GetSpan(0);
        buffer.Advance(0);

        buffer.Length.Should().Be(0);
    }

    [Fact]
    public void PacketBuffer_ShouldHandleVeryLargeData()
    {
        var buffer = new PacketBuffer();
        var largeData = new byte[100_000];
        new Random(123).NextBytes(largeData);

        var span = buffer.GetSpan(largeData.Length);
        largeData.CopyTo(span);
        buffer.Advance(largeData.Length);

        buffer.Length.Should().Be(largeData.Length);
        var result = buffer.ToArray();
        result.Should().Equal(largeData);
    }

    [Fact]
    public async Task PacketBuffer_ShouldBeThreadSafe_WithLocking()
    {
        var buffer = new PacketBuffer();
        var tasks = new List<Task>();
        var random = new Random(42);

        for (int i = 0; i < 10; i++)
        {
            int taskId = i;
            tasks.Add(Task.Run(() =>
            {
                lock (buffer)
                {
                    var data = new byte[] { (byte)taskId };
                    var span = buffer.GetSpan(data.Length);
                    data.CopyTo(span);
                    buffer.Advance(data.Length);
                }
            }));
        }

        await Task.WhenAll(tasks.ToArray());

        buffer.Length.Should().Be(10);
    }

    [Fact]
    public void PacketBuffer_ShouldHandleIncrementalResize()
    {
        var buffer = new PacketBuffer();
        
        for (int i = 0; i < 1000; i++)
        {
            var data = new byte[] { (byte)(i % 256) };
            var span = buffer.GetSpan(data.Length);
            data.CopyTo(span);
            buffer.Advance(data.Length);
        }

        buffer.Length.Should().Be(1000);
        var result = buffer.ToArray();
        result.Length.Should().Be(1000);
        
        for (int i = 0; i < 1000; i++)
        {
            result[i].Should().Be((byte)(i % 256));
        }
    }

    [Fact]
    public void PacketBuffer_ToArray_ShouldNotAffectInternalState()
    {
        var buffer = new PacketBuffer();
        var data = new byte[] { 1, 2, 3, 4, 5 };

        var span = buffer.GetSpan(data.Length);
        data.CopyTo(span);
        buffer.Advance(data.Length);

        var array1 = buffer.ToArray();
        var array2 = buffer.ToArray();

        array1.Should().Equal(array2);
        buffer.Length.Should().Be(data.Length);
    }

    [Fact]
    public void PacketBuffer_ShouldHandleAlternatingWriteAndReset()
    {
        var buffer = new PacketBuffer();

        for (int iteration = 0; iteration < 100; iteration++)
        {
            var data = new byte[] { (byte)iteration };
            var span = buffer.GetSpan(data.Length);
            data.CopyTo(span);
            buffer.Advance(data.Length);

            buffer.Length.Should().Be(1);
            buffer.ToArray().Should().Equal(data);

            buffer.Reset();
            buffer.Length.Should().Be(0);
        }
    }
}
