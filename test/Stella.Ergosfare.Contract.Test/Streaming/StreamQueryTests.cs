using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Generated;
using Stella.Ergosfare.Queries.Abstractions;
using Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection;

namespace Stella.Ergosfare.Contract.Test.Streaming;

/// <summary>
/// The streaming surface as it stands today, pinned shallowly on purpose: it is marked
/// experimental, so only the two observations a caller can rely on are recorded here —
/// items arrive in order, and a mid-stream failure surfaces after the items that preceded
/// it.
/// </summary>
public sealed class StreamQueryTests
{
    private const string Key = "contract.stream";

    /// <summary>Thrown from the middle of a stream.</summary>
    public sealed class StreamFailure() : Exception("stream failed");

    [DiscoveryKey(Key)]
    public sealed class Ticks : IStreamQuery<int>
    {
        /// <summary>Whether the handler stops mid-stream by throwing.</summary>
        public bool FailMidway;
    }

    [DiscoveryKey(Key)]
    public sealed class TicksHandler : IStreamQueryHandler<Ticks, int>
    {
        public async IAsyncEnumerable<int> StreamAsync(Ticks query, IExecutionContext context)
        {
            yield return 1;

            // A real await between items, so the scenario is not a synchronous shortcut.
            await Task.Yield();
            yield return 2;

            if (query.FailMidway)
            {
                throw new StreamFailure();
            }

            yield return 3;
        }
    }

    private static ServiceProvider CreateProvider()
        => new ServiceCollection()
            .AddErgosfare(options => options.AddQueryModule(queries => queries.RegisterGenerated(Key)))
            .BuildServiceProvider();

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_stream_query_yields_its_items_in_order_and_completes()
    {
        await using var provider = CreateProvider();
        var received = new List<int>();

        await foreach (var tick in provider.GetRequiredService<IQueryMediator>().StreamAsync(new Ticks()))
        {
            received.Add(tick);
        }

        Assert.Equal([1, 2, 3], received);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_mid_stream_exception_surfaces_unwrapped_after_the_items_that_preceded_it()
    {
        await using var provider = CreateProvider();
        var mediator = provider.GetRequiredService<IQueryMediator>();
        var received = new List<int>();

        await Assert.ThrowsAsync<StreamFailure>(async () =>
        {
            await foreach (var tick in mediator.StreamAsync(new Ticks { FailMidway = true }))
            {
                received.Add(tick);
            }
        });

        Assert.Equal([1, 2], received);
    }
}
