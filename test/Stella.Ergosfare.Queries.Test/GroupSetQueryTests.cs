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

// The fixtures live at the top level so the source generator compiles the per-set plans
// the GroupSet call sites name; every type is owned by GroupSetQueryTests alone, and the
// container below registers each query's full compiled pipeline.

public sealed record GsqRoutedQuery : IQuery<string>;

[Group("gsq.east")]
public sealed class GsqEastRoutedHandler : IQueryHandler<GsqRoutedQuery, string>
{
    public ValueTask<string> HandleAsync(GsqRoutedQuery query, ErgosfareContext context)
        => ValueTask.FromResult("east");
}

[Group("gsq.west")]
public sealed class GsqWestRoutedHandler : IQueryHandler<GsqRoutedQuery, string>
{
    public ValueTask<string> HandleAsync(GsqRoutedQuery query, ErgosfareContext context)
        => ValueTask.FromResult("west");
}

public sealed record GsqDefaultQuery : IQuery<string>;

public sealed class GsqDefaultQueryHandler : IQueryHandler<GsqDefaultQuery, string>
{
    public ValueTask<string> HandleAsync(GsqDefaultQuery query, ErgosfareContext context)
        => ValueTask.FromResult("default");
}

public sealed record GsqRoutedStreamQuery : IStreamQuery<string>;

[Group("gsq.east")]
public sealed class GsqEastRoutedStreamHandler : IStreamQueryHandler<GsqRoutedStreamQuery, string>
{
    public async IAsyncEnumerable<string> StreamAsync(GsqRoutedStreamQuery query, ErgosfareContext context)
    {
        await Task.Yield();
        yield return "east";
    }
}

[Group("gsq.west")]
public sealed class GsqWestRoutedStreamHandler : IStreamQueryHandler<GsqRoutedStreamQuery, string>
{
    public async IAsyncEnumerable<string> StreamAsync(GsqRoutedStreamQuery query, ErgosfareContext context)
    {
        await Task.Yield();
        yield return "west";
    }
}

/// <summary>
/// The <see cref="GroupSet"/> overloads on the query mediator: result queries filter
/// through their per-set plans, alternation stays correct, the empty set routes to the
/// default pipeline — and a stream naming a set is refused, since no per-set stream plans
/// are compiled yet.
/// </summary>
public class GroupSetQueryTests
{
    private static readonly GroupSet East = GroupSet.Of("gsq.east");
    private static readonly GroupSet West = GroupSet.Of("gsq.west");

    private static ServiceProvider Build()
        => new ServiceCollection()
            .AddErgosfare(x => x.AddQueryModule(q =>
            {
                q.Register<GsqEastRoutedHandler>();
                q.Register<GsqWestRoutedHandler>();
                q.Register<GsqDefaultQueryHandler>();
                q.Register<GsqEastRoutedStreamHandler>();
                q.Register<GsqWestRoutedStreamHandler>();
            }))
            .BuildServiceProvider();

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task QueryOverload_FiltersAndSurvivesRepetitionAndAlternation()
    {
        await using var provider = Build();
        var mediator = provider.GetRequiredService<IQueryMediator>();

        Assert.Equal("east", await mediator.QueryAsync(new GsqRoutedQuery(), East));
        Assert.Equal("east", await mediator.QueryAsync(new GsqRoutedQuery(), East));
        Assert.Equal("west", await mediator.QueryAsync(new GsqRoutedQuery(), West));
        Assert.Equal("east", await mediator.QueryAsync(new GsqRoutedQuery(), East));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task QueryOverload_EmptySet_DispatchesTheDefaultPipeline()
    {
        await using var provider = Build();
        var mediator = provider.GetRequiredService<IQueryMediator>();

        Assert.Equal("default", await mediator.QueryAsync(new GsqDefaultQuery(), GroupSet.Empty));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task StreamOverload_NamedSet_IsRefusedAsUnplanned()
    {
        await using var provider = Build();
        var mediator = provider.GetRequiredService<IQueryMediator>();

        // Streams have no per-set plans yet, and a pair whose handlers all live in named
        // groups has no default plan either — so the GroupSet overload fails as unplanned
        // rather than selecting participants at run time.
        var thrown = await Assert.ThrowsAsync<UnplannedDispatchException>(async () =>
        {
            await foreach (var _ in mediator.StreamAsync(new GsqRoutedStreamQuery(), East))
            {
            }
        });

        Assert.Equal(UnplannedDispatchReason.NoCompiledPlan, thrown.Reason);
    }
}
