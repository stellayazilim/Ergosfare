#pragma warning disable ERGOEXP003
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using Stella.Ergosfare.Core.Abstractions.Streaming;

namespace Stella.Ergosfare.Core.Test;

[Trait("Category", "Unit")]
public class StreamInputPipeTests
{
    private sealed class LegacyInput() : ErgosfareStream<int>(1);

    [Fact]
    public async Task RootDisposal_ReleasesPendingWriter_AndIsIdempotent()
    {
        ErgosfareStream stream = new LegacyInput();
        var input = (LegacyInput)stream;
        await input.WriteAsync(1);
        var blocked = input.WriteAsync(2).AsTask();
        Assert.False(blocked.IsCompleted);
        await stream.DisposeAsync();
        await stream.DisposeAsync();
        await Assert.ThrowsAsync<Stella.Ergosfare.Core.Abstractions.Exceptions.StreamOutputDisposedException>(
            async () => await blocked.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Throws<ObjectDisposedException>(() => input.GetAsyncEnumerator());
    }

    [Fact]
    public async Task RootDisposal_WakesWaitingReader()
    {
        var input = new LegacyInput();
        await using var reader = input.GetAsyncEnumerator();
        var pending = reader.MoveNextAsync().AsTask();
        await input.DisposeAsync();
        var error = await Record.ExceptionAsync(async () => await pending.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.NotNull(error);
        Assert.IsNotType<TimeoutException>(error);
    }

    [Fact]
    public async Task StagingGrowsBeyondQueueCapacity_WhileFlushStillWaitsForReader()
    {
        await using var input = new Input<int>(1);
        IBufferWriter<int> writer = input;
        writer.GetMemory(1).Span[0] = 1;
        writer.Advance(1);
        var more = writer.GetMemory(20);
        Assert.True(more.Length >= 20);
        more.Span[0] = 2;
        more.Span[1] = 3;
        writer.Advance(2);
        var flush = input.FlushAsync();
        Assert.False(flush.IsCompleted);
        var drain = Drain(input);
        await flush;
        input.Complete();
        Assert.Equal(new[] { 1, 2, 3 }, await drain);
    }

    private sealed class Input<T>(int capacity = 2) : StreamInput<T, Input<T>>(capacity);
    private sealed class LengthConverter : IPipeConverter<string, int>
    {
        public int Convert(string chunk) => chunk.Length;
    }

    [Fact]
    public async Task PipeStartsBeforeConsumption_AndCapacityBoundsQueuedItems()
    {
        int produced = 0;
        var disposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async IAsyncEnumerable<int> Source([EnumeratorCancellation] CancellationToken ct = default)
        {
            try
            {
                for (var i = 0; i < 10; i++) { ct.ThrowIfCancellationRequested(); produced++; yield return i; }
                await Task.CompletedTask;
            }
            finally { disposed.SetResult(); }
        }
        await using var input = new Input<int>(1);
        Assert.Same(input, input.Pipe(Source()));
        Assert.Equal(2, produced); // One queued item and one waiting write.
        Assert.False(disposed.Task.IsCompleted);
        Assert.Equal(Enumerable.Range(0, 10), await Drain(input));
        await disposed.Task.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task DisposeBeforeDispatch_CancelsAndDisposesBoundSource()
    {
        var disposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async IAsyncEnumerable<int> Source([EnumeratorCancellation] CancellationToken ct = default)
        {
            try { yield return 1; await Task.Delay(Timeout.Infinite, ct); }
            finally { disposed.SetResult(); }
        }
        var input = new Input<int>().Pipe(Source());
        await ((IAsyncDisposable)input).DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(disposed.Task.IsCompleted);
    }

    [Fact]
    public async Task FaultBeforeDispatch_PreservesOriginalException()
    {
        var error = new InvalidOperationException("source failed");
        async IAsyncEnumerable<int> Source()
        {
            await Task.CompletedTask;
            if (error != null) throw error;
            yield break;
        }
        await using var input = new Input<int>().Pipe(Source());
        Assert.Same(error, await Assert.ThrowsAsync<InvalidOperationException>(() => Drain(input)));
    }

    [Fact]
    public async Task DelegatesAndChunkConverter_ProduceOneOutputPerInput()
    {
        await using var mapped = new Input<int>().TransformPipe(Items("a", "abc"), x => x.Length);
        Assert.Equal(new[] { 1, 3 }, await Drain(mapped));
        await using var converted = new Input<int>().TransformPipe(Items("a", "abc"), new LengthConverter());
        Assert.Equal(new[] { 1, 3 }, await Drain(converted));
        var sequence = 0;
        await using var factory = new Input<int>().TransformPipe(Items("a", "abc"), () => ++sequence);
        Assert.Equal(new[] { 1, 2 }, await Drain(factory));
        await using var same = new Input<int>().Pipe(Items(1, 2), x => x * 2);
        Assert.Equal(new[] { 2, 4 }, await Drain(same));
    }

    [Fact]
    public async Task ByteOverloads_LeaveSourceOpen_AndBlocksKeepTheirContents()
    {
        using var source = new MemoryStream(new byte[] { 1, 2, 3 });
        await using var bytes = new Input<byte>().Pipe(source);
        Assert.Equal(new byte[] { 1, 2, 3 }, await Drain(bytes));
        Assert.True(source.CanRead);
        source.Position = 0;
        await using var mapped = new Input<int>().Pipe(source, (byte x) => x + 1);
        Assert.Equal(new[] { 2, 3, 4 }, await Drain(mapped));
        source.Position = 0;
        await using var blocks = new Input<ReadOnlyMemory<byte>>().Pipe(source, chunkSize: 1);
        var items = await Drain(blocks);
        Assert.Equal(new byte[] { 1, 2, 3 }, items.SelectMany(x => x.ToArray()));
        Assert.True(source.CanRead);
    }

    private sealed class SlowReadStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => base.ReadAsync(buffer[..Math.Min(1, buffer.Length)], cancellationToken);
    }

    [Fact]
    public async Task LineConverter_PreservesSplitUtf8CharactersAndLeavesSourceOpen()
    {
        using var source = new SlowReadStream(Encoding.UTF8.GetBytes("çağrı🙂\r\nson"));
        await using var input = new Input<string>().Pipe(source, PipeConverters.Lines());
        Assert.Equal(new[] { "çağrı🙂", "son" }, await Drain(input));
        Assert.True(source.CanRead);
    }

    [Fact]
    public async Task BufferWriter_FlushesStagedItems_AndRejectsMixedSource()
    {
        await using var input = new Input<string>(2);
        IBufferWriter<string> writer = input;
        writer.GetMemory(2).Span[0] = "a";
        writer.GetMemory(2).Span[1] = "b";
        writer.Advance(2);
        Assert.Throws<InvalidOperationException>(() => input.Complete());
        Assert.Throws<InvalidOperationException>(() => input.Pipe(Items("c")));
        await input.FlushAsync();
        input.Complete();
        Assert.Equal(new[] { "a", "b" }, await Drain(input));
    }

    [Fact]
    public async Task PipeRejectsSecondSourceAndManualWrites()
    {
        await using var input = new Input<int>().Pipe(Items(1));
        Assert.Throws<InvalidOperationException>(() => input.Pipe(Items(2)));
        Assert.Throws<InvalidOperationException>(() => input.TryWrite(2));
        Assert.Equal(new[] { 1 }, await Drain(input));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Input<object>(0));
    }

    private static async IAsyncEnumerable<T> Items<T>(params T[] items)
    {
        foreach (var item in items) yield return item;
        await Task.CompletedTask;
    }

    private static async Task<List<T>> Drain<T>(IAsyncEnumerable<T> source)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var result = new List<T>();
        await foreach (var item in source.WithCancellation(timeout.Token)) result.Add(item);
        return result;
    }
}
