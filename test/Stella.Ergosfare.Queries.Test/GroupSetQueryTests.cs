using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Queries.Abstractions;
using Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Queries.Test;

/// <summary>
/// The <see cref="GroupSet"/> overloads on the query mediator: result queries and
/// streams filter through the canonical slot fast path, alternation stays correct, and
/// the empty set routes to the default pipeline. Helper types are excluded from
/// discovery so assembly scans cannot alter these pipelines.
/// </summary>
public class GroupSetQueryTests
{
    private static readonly GroupSet East = GroupSet.Of("gsq.east");
    private static readonly GroupSet West = GroupSet.Of("gsq.west");

    [ExcludeFromDiscovery]
    public sealed record RoutedQuery : IQuery<string>;

    [ExcludeFromDiscovery]
    [Group("gsq.east")]
    public sealed class EastRoutedHandler : IQueryHandler<RoutedQuery, string>
    {
        public ValueTask<string> HandleAsync(RoutedQuery query, IExecutionContext context)
            => ValueTask.FromResult("east");
    }

    [ExcludeFromDiscovery]
    [Group("gsq.west")]
    public sealed class WestRoutedHandler : IQueryHandler<RoutedQuery, string>
    {
        public ValueTask<string> HandleAsync(RoutedQuery query, IExecutionContext context)
            => ValueTask.FromResult("west");
    }

    [ExcludeFromDiscovery]
    public sealed record DefaultQuery : IQuery<string>;

    [ExcludeFromDiscovery]
    public sealed class DefaultQueryHandler : IQueryHandler<DefaultQuery, string>
    {
        public ValueTask<string> HandleAsync(DefaultQuery query, IExecutionContext context)
            => ValueTask.FromResult("default");
    }

    [ExcludeFromDiscovery]
    public sealed record RoutedStreamQuery : IStreamQuery<string>;

    [ExcludeFromDiscovery]
    [Group("gsq.east")]
    public sealed class EastRoutedStreamHandler : IStreamQueryHandler<RoutedStreamQuery, string>
    {
        public async IAsyncEnumerable<string> StreamAsync(RoutedStreamQuery query, IExecutionContext context)
        {
            await Task.Yield();
            yield return "east";
        }
    }

    [ExcludeFromDiscovery]
    [Group("gsq.west")]
    public sealed class WestRoutedStreamHandler : IStreamQueryHandler<RoutedStreamQuery, string>
    {
        public async IAsyncEnumerable<string> StreamAsync(RoutedStreamQuery query, IExecutionContext context)
        {
            await Task.Yield();
            yield return "west";
        }
    }

    private static ServiceProvider Build()
        => new ServiceCollection()
            .AddErgosfare(x => x.AddQueryModule(q =>
            {
                q.Register<EastRoutedHandler>();
                q.Register<WestRoutedHandler>();
                q.Register<DefaultQueryHandler>();
                q.Register<EastRoutedStreamHandler>();
                q.Register<WestRoutedStreamHandler>();
            }))
            .BuildServiceProvider();

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task QueryOverload_FiltersAndSurvivesRepetitionAndAlternation()
    {
        await using var provider = Build();
        var mediator = provider.GetRequiredService<IQueryMediator>();

        Assert.Equal("east", await mediator.QueryAsync(new RoutedQuery(), East));
        Assert.Equal("east", await mediator.QueryAsync(new RoutedQuery(), East));
        Assert.Equal("west", await mediator.QueryAsync(new RoutedQuery(), West));
        Assert.Equal("east", await mediator.QueryAsync(new RoutedQuery(), East));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task QueryOverload_EmptySet_DispatchesTheDefaultPipeline()
    {
        await using var provider = Build();
        var mediator = provider.GetRequiredService<IQueryMediator>();

        Assert.Equal("default", await mediator.QueryAsync(new DefaultQuery(), GroupSet.Empty));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task StreamOverload_FiltersAndSurvivesAlternation()
    {
        await using var provider = Build();
        var mediator = provider.GetRequiredService<IQueryMediator>();

        Assert.Equal("east", await StreamOne(mediator, East));
        Assert.Equal("west", await StreamOne(mediator, West));
        Assert.Equal("east", await StreamOne(mediator, East));

        static async Task<string> StreamOne(IQueryMediator mediator, GroupSet groups)
        {
            await foreach (var item in mediator.StreamAsync(new RoutedStreamQuery(), groups))
            {
                return item;
            }

            throw new InvalidOperationException("stream yielded nothing");
        }
    }
}
