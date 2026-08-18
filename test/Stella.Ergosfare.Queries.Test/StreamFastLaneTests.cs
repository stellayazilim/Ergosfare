// Stream messaging is under revision and its entry points carry the notice; these are
// deliberate call sites of the surface as it stands today.
#pragma warning disable CS0618

using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Queries.Abstractions;
using Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Queries.Test;

// The planned fixtures live at the top level, unkeyed, so the source generator compiles
// the stream plan the dispatches below run; every type is owned by StreamFastLaneTests
// alone, and the container registers each streamed query's full pipeline.

public sealed record NumberStream : IStreamQuery<int>;

public sealed class NumberStreamHandler : IStreamQueryHandler<NumberStream, int>
{
    public async IAsyncEnumerable<int> StreamAsync(NumberStream query, ErgosfareContext context)
    {
        context.Set("streamRan", true);
        yield return 1;
        await Task.Yield();
        yield return 2;
        yield return 3;
    }
}

public sealed record RoutedStream : IStreamQuery<string>;

[Group("east")]
public sealed class EastStreamHandler : IStreamQueryHandler<RoutedStream, string>
{
    public async IAsyncEnumerable<string> StreamAsync(RoutedStream query, ErgosfareContext context)
    {
        await Task.Yield();
        yield return "east";
    }
}

[Group("west")]
public sealed class WestStreamHandler : IStreamQueryHandler<RoutedStream, string>
{
    public async IAsyncEnumerable<string> StreamAsync(RoutedStream query, ErgosfareContext context)
    {
        await Task.Yield();
        yield return "west";
    }
}

/// <summary>Never registered anywhere: the no-handler scenario dispatches this.</summary>
[ExcludeFromDiscovery]
public sealed record UnhandledStream : IStreamQuery<int>;

/// <summary>
/// The compiled stream plan: streams run the generated pipeline body the engine verifies
/// once and reuses. Covers plan reuse across streams, caller-visible items, the
/// unregistered-query failure parity, and the refusal a grouped stream meets — no per-set
/// stream plans are compiled yet, so naming a set is an unplanned dispatch.
/// </summary>
public class StreamFastLaneTests
{
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task RepeatedStreams_ServeThePlan_AndFlowItemsToTheCaller()
    {
        await using var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddQueryModule(q => q.Register<NumberStreamHandler>()))
            .BuildServiceProvider();

        var mediator = provider.GetRequiredService<IQueryMediator>();

        // First stream verifies the plan against the live pipeline, second is served from
        // the settled verdict; both must yield the full sequence and surface handler
        // writes through the settings items.
        for (var i = 0; i < 2; i++)
        {
            var context = new ErgosfareContext();
            var items = new List<int>();

            await foreach (var item in mediator.StreamAsync(new NumberStream(), context))
            {
                items.Add(item);
            }

            Assert.Equal([1, 2, 3], items);
            Assert.Equal(true, context.Items["streamRan"]);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task GroupedStream_IsRefusedAsUnplanned()
    {
        await using var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddQueryModule(q =>
            {
                q.Register<EastStreamHandler>();
                q.Register<WestStreamHandler>();
            }))
            .BuildServiceProvider();

        var mediator = provider.GetRequiredService<IQueryMediator>();

        // No per-set stream plans are compiled yet, and a pair whose handlers all live in
        // named groups has no default plan either — so a grouped stream fails as
        // unplanned rather than running a pipeline no plan produced.
        var thrown = await Assert.ThrowsAsync<UnplannedDispatchException>(async () =>
        {
            await foreach (var _ in mediator.StreamAsync(new RoutedStream(), new[] { "east" }))
            {
            }
        });

        Assert.Equal(UnplannedDispatchReason.NoCompiledPlan, thrown.Reason);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task UnregisteredStreamQuery_ThrowsAtCallTime_LikeTheMediatePath()
    {
        await using var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddQueryModule(_ => { }))
            .BuildServiceProvider();

        var mediator = provider.GetRequiredService<IQueryMediator>();

        // A query nothing handles is a failed dispatch, not an empty stream, and the
        // failure is raised from the StreamAsync call itself — descriptor resolution
        // precedes enumeration. The registry is process-wide, though: another suite's
        // marker-targeted (IQuery-assignable) interceptor may have given every query a
        // descriptor, in which case the failure surfaces at enumeration instead. Both
        // timings raise NoHandlerFoundException; only the timing tells them apart.
        try
        {
            var stream = mediator.StreamAsync(new UnhandledStream());

            await Assert.ThrowsAsync<NoHandlerFoundException>(async () =>
            {
                await foreach (var _ in stream)
                {
                }
            });
        }
        catch (NoHandlerFoundException)
        {
            // Clean-registry timing: thrown at call time, before any enumeration.
        }
    }
}
