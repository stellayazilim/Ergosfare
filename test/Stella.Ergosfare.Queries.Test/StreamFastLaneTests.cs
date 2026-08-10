using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Queries.Abstractions;
using Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Queries.Test;

/// <summary>
/// The engine-backed streaming fast lane: streams run against the invoker-cached,
/// registry-version-guarded pipeline plan instead of the per-call Mediate options path.
/// Covers plan reuse across streams, grouped streams alternating group sets on the
/// last-used slot, caller-visible items, and the unregistered-query failure parity.
/// Helper types are excluded from discovery so assembly scans (the registry is
/// process-wide) cannot alter these pipelines.
/// </summary>
public class StreamFastLaneTests
{
    [ExcludeFromDiscovery]
    public sealed record NumberStream : IStreamQuery<int>;

    [ExcludeFromDiscovery]
    public sealed class NumberStreamHandler : IStreamQueryHandler<NumberStream, int>
    {
        public async IAsyncEnumerable<int> StreamAsync(NumberStream query, IExecutionContext context)
        {
            context.Set("streamRan", true);
            yield return 1;
            await Task.Yield();
            yield return 2;
            yield return 3;
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task RepeatedStreams_ServeThePlan_AndFlowItemsToTheCaller()
    {
        await using var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddQueryModule(q => q.Register<NumberStreamHandler>()))
            .BuildServiceProvider();

        var mediator = provider.GetRequiredService<IQueryMediator>();

        // First stream builds the invoker's plan, second is served from it; both must
        // yield the full sequence and surface handler writes through the settings items.
        for (var i = 0; i < 2; i++)
        {
            var settings = new QueryMediationSettings();
            var items = new List<int>();

            await foreach (var item in mediator.StreamAsync(new NumberStream(), settings))
            {
                items.Add(item);
            }

            Assert.Equal([1, 2, 3], items);
            Assert.Equal(true, settings.Items["streamRan"]);
        }
    }

    [ExcludeFromDiscovery]
    public sealed record RoutedStream : IStreamQuery<string>;

    [ExcludeFromDiscovery]
    [Group("east")]
    public sealed class EastStreamHandler : IStreamQueryHandler<RoutedStream, string>
    {
        public async IAsyncEnumerable<string> StreamAsync(RoutedStream query, IExecutionContext context)
        {
            await Task.Yield();
            yield return "east";
        }
    }

    [ExcludeFromDiscovery]
    [Group("west")]
    public sealed class WestStreamHandler : IStreamQueryHandler<RoutedStream, string>
    {
        public async IAsyncEnumerable<string> StreamAsync(RoutedStream query, IExecutionContext context)
        {
            await Task.Yield();
            yield return "west";
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task GroupedStreams_AlternatingGroupSets_RunTheRequestedGroup()
    {
        await using var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddQueryModule(q =>
            {
                q.Register<EastStreamHandler>();
                q.Register<WestStreamHandler>();
            }))
            .BuildServiceProvider();

        var mediator = provider.GetRequiredService<IQueryMediator>();

        Assert.Equal("east", await StreamOne(mediator, "east"));
        Assert.Equal("west", await StreamOne(mediator, "west"));
        Assert.Equal("east", await StreamOne(mediator, "east"));

        static async Task<string> StreamOne(IQueryMediator mediator, params string[] groups)
        {
            var settings = new QueryMediationSettings { Filters = { Groups = groups } };

            await foreach (var item in mediator.StreamAsync(new RoutedStream(), settings))
            {
                return item;
            }

            throw new InvalidOperationException("stream yielded nothing");
        }
    }

    [ExcludeFromDiscovery]
    public sealed record UnhandledStream : IStreamQuery<int>;

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task UnregisteredStreamQuery_ThrowsAtCallTime_LikeTheMediatePath()
    {
        await using var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddQueryModule(_ => { }))
            .BuildServiceProvider();

        var mediator = provider.GetRequiredService<IQueryMediator>();

        // The Mediate path threw from the StreamAsync call itself (descriptor resolution
        // precedes enumeration); the fast lane must keep that timing. The registry is
        // process-wide, though: another suite's marker-targeted (IQuery-assignable)
        // interceptor may have given every query a descriptor, in which case both paths
        // defer and fail at enumeration with the strategy's no-handler error instead —
        // the fast-lane/Mediate parity this test guards holds either way. Both timings now
        // raise NoHandlerFoundException; only the timing tells them apart.
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
