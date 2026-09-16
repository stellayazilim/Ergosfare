#pragma warning disable ERGOEXP003
using Stella.Ergosfare.Core.Abstractions.Streaming;

namespace Stella.Ergosfare.Core.Test;

[Trait("Category", "Unit")]
public class ByteStreamBoundaryTests
{
    [Fact]
    public async Task StreamFacade_IsForwardOnlyAndSkipsEmptyChunks()
    {
        var disposed = false;
        var started = false;
        async IAsyncEnumerable<ReadOnlyMemory<byte>> Chunks()
        {
            started = true;
            try
            {
                yield return ReadOnlyMemory<byte>.Empty;
                yield return new byte[] { 1, 2, 3 };
                await Task.Yield();
                yield return ReadOnlyMemory<byte>.Empty;
                yield return new byte[] { 4 };
            }
            finally { disposed = true; }
        }
        await using (var stream = Chunks().AsStream())
        {
            Assert.True(stream.CanRead);
            Assert.False(stream.CanSeek);
            Assert.False(stream.CanWrite);
            Assert.Throws<NotSupportedException>(() => stream.Length);
            Assert.Throws<NotSupportedException>(() => stream.Position);
            Assert.Throws<NotSupportedException>(() => stream.Position = 1);
            Assert.Throws<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
            Assert.Throws<NotSupportedException>(() => stream.SetLength(4));
            Assert.Throws<NotSupportedException>(() => stream.Write([1], 0, 1));
            Assert.Throws<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
            stream.Flush();
            Assert.Equal(0, await stream.ReadAsync(Memory<byte>.Empty));
            Assert.False(started);
            var buffer = new byte[5];
            Assert.Equal(2, await stream.ReadAsync(buffer, 1, 2, default));
            Assert.Equal(new byte[] { 0, 1, 2, 0, 0 }, buffer);
            Assert.Equal(1, await stream.ReadAsync(buffer.AsMemory(3, 2)));
            Assert.Equal(3, buffer[3]);
            Assert.Equal(1, await stream.ReadAsync(buffer.AsMemory(4, 1)));
            Assert.Equal(4, buffer[4]);
            Assert.Equal(0, await stream.ReadAsync(buffer));
            Assert.Equal(0, await stream.ReadAsync(buffer));
        }
        Assert.True(disposed);
    }

    [Fact]
    public async Task EarlyDisposal_ReleasesTheChunkEnumerator()
    {
        var disposed = false;
        async IAsyncEnumerable<ReadOnlyMemory<byte>> Chunks()
        {
            try { yield return new byte[] { 1, 2 }; await Task.Yield(); yield return new byte[] { 3 }; }
            finally { disposed = true; }
        }
        var stream = Chunks().AsStream();
        Assert.Equal(1, await stream.ReadAsync(new byte[1]));
        await stream.DisposeAsync();
        Assert.True(disposed);
    }
}
