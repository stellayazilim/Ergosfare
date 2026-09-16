#pragma warning disable ERGOEXP003
using Stella.Ergosfare.Core.Abstractions.Streaming;
using Stella.Ergosfare.Commands.Abstractions.Streaming;
using Stella.Ergosfare.Queries.Abstractions.Streaming;

namespace Stella.Ergosfare.Contract.Test.Streaming;

[Trait("Category", "Contract")]
public class StreamCompatibilityBoundaryTests
{
    private sealed class Input() : StreamInput<int, Input>(4);
    private sealed class DoubleConverter : IPipeConverter<int, int>, IPipeConverter<byte, int>
    {
        public int Convert(int value) => value * 2;
        public int Convert(byte value) => value * 2;
    }
    [Stella.Ergosfare.Core.Abstractions.Attributes.ExcludeFromDiscovery]
    private sealed class Command : ErgosfareCommandStream<int, string>
    {
        public Command(string meta) : base(meta) { }
        public Command(string meta, IAsyncEnumerable<int> source) : base(meta, source) { }
    }
    [Stella.Ergosfare.Core.Abstractions.Attributes.ExcludeFromDiscovery]
    private sealed class Query : ErgosfareQueryStream<int, string, int>
    {
        public Query(string meta) : base(meta) { }
        public Query(string meta, IAsyncEnumerable<int> source) : base(meta, source) { }
    }

    [Fact]
    public async Task MetadataWrappers_PreserveManualAndAdoptedInputs()
    {
        await using var command = new Command("initial");
        command.Meta = "normalized";
        Assert.Equal("normalized", command.Meta);
        Assert.True(command.TryWrite(7));
        command.Complete();
        Assert.Equal(new[] { 7 }, await Drain(command));
        await using var query = new Query("initial");
        query.Meta = "normalized";
        Assert.Equal("normalized", query.Meta);
        await query.WriteAsync(8);
        query.Complete();
        Assert.Equal(new[] { 8 }, await Drain(query));
        await using var adoptedCommand = new Command("source", Items());
        await using var adoptedQuery = new Query("source", Items());
        Assert.Equal("source", adoptedCommand.Meta);
        Assert.Equal("source", adoptedQuery.Meta);
        Assert.Equal(new[] { 1, 2 }, await Drain(adoptedCommand));
        Assert.Equal(new[] { 1, 2 }, await Drain(adoptedQuery));
        Assert.Throws<ArgumentNullException>(() => new Command(null!));
        Assert.Throws<ArgumentNullException>(() => new Query(null!, Items()));
    }

    [Fact]
    public async Task ConverterOverloads_ProduceExactlyOneItemPerSourceItem()
    {
        await using var factory = new Input().Pipe(Items(), () => 3);
        Assert.Equal(new[] { 3, 3 }, await Drain(factory));
        await using var converter = new Input().Pipe(Items(), new DoubleConverter());
        Assert.Equal(new[] { 2, 4 }, await Drain(converter));
        using var source = new MemoryStream([1, 2]);
        await using var byteFactory = new Input().Pipe(source, () => 4);
        Assert.Equal(new[] { 4, 4 }, await Drain(byteFactory));
        source.Position = 0;
        await using var byteConverter = new Input().Pipe(source, new DoubleConverter());
        Assert.Equal(new[] { 2, 4 }, await Drain(byteConverter));
        Assert.True(source.CanRead);
    }

    [Fact]
    public async Task FaultedInput_PreservesTheOriginalErrorForBothWriterForms()
    {
        await using var input = new Input();
        var error = new InvalidOperationException("source failed");
        input.Fault(error);
        Assert.Same(error, Assert.Throws<InvalidOperationException>(() => input.TryWrite(1)));
        Assert.Same(error, await Assert.ThrowsAsync<InvalidOperationException>(async () => await input.WriteAsync(1)));
        Assert.Same(error, await Assert.ThrowsAsync<InvalidOperationException>(() => Drain(input)));
    }

    private static async IAsyncEnumerable<int> Items()
    { yield return 1; await Task.Yield(); yield return 2; }
    private static async Task<int[]> Drain(IAsyncEnumerable<int> source)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var items = new List<int>();
        await foreach (var item in source.WithCancellation(timeout.Token)) items.Add(item);
        return items.ToArray();
    }
}
