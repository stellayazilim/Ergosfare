using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Queries;
using Stella.Ergosfare.Queries.Abstractions;
using Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection;

namespace Stella.Ergosfare.Queries.Test;

// The fixtures live at the top level so the source generator compiles the default and
// per-set plans the calls below run; every type is owned by TypedQueryFacadeConvenienceTests
// alone, and the container below registers each query's full compiled pipeline.

public sealed record TqfRoutedQuery : IQuery<string>;

[Group("tqf.east")]
public sealed class TqfEastRoutedHandler : IQueryHandler<TqfRoutedQuery, string>
{
    public ValueTask<string> HandleAsync(TqfRoutedQuery query, ErgosfareContext context)
        => ValueTask.FromResult("east");
}

[Group("tqf.west")]
public sealed class TqfWestRoutedHandler : IQueryHandler<TqfRoutedQuery, string>
{
    public ValueTask<string> HandleAsync(TqfRoutedQuery query, ErgosfareContext context)
        => ValueTask.FromResult("west");
}

public sealed record TqfPlainQuery : IQuery<string>;

public sealed class TqfPlainQueryHandler : IQueryHandler<TqfPlainQuery, string>
{
    public ValueTask<string> HandleAsync(TqfPlainQuery query, ErgosfareContext context)
    {
        context.Set("tqf.token", context.CancellationToken);
        return ValueTask.FromResult("plain");
    }
}

/// <summary>
/// The typed conveniences carried by <see cref="QueryMediator"/> itself, exercised through a
/// facade-typed receiver — which is the only way to reach them, since a concrete-typed call
/// site never sees the interface's default implementations.
/// </summary>
/// <remarks>
/// The interface's defaults forward to the untyped calls and would return the same answers,
/// so a result alone cannot tell the two apart. What these pin is that the facade's own
/// bodies route correctly: same filtering as the untyped lane, the caller's context arriving
/// intact on the nested-dispatch path, and a null <see cref="GroupSet"/> rejected at the call
/// site rather than turned into an unfiltered dispatch.
/// </remarks>
public class TypedQueryFacadeConvenienceTests
{
    private static readonly GroupSet East = GroupSet.Of("tqf.east");
    private static readonly GroupSet West = GroupSet.Of("tqf.west");

    private static ServiceProvider Build()
        => new ServiceCollection()
            .AddErgosfare(x => x.AddQueryModule(q =>
            {
                q.Register<TqfEastRoutedHandler>();
                q.Register<TqfWestRoutedHandler>();
                q.Register<TqfPlainQueryHandler>();
            }))
            .BuildServiceProvider();

    private static QueryMediator Facade(IServiceProvider provider)
        => Assert.IsAssignableFrom<QueryMediator>(provider.GetRequiredService<IQueryMediator>());

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task TypedQuery_UnderACallerOwnedContext_RunsWithThatContextsCancellation()
    {
        await using var provider = Build();
        var mediator = Facade(provider);
        using var cts = new CancellationTokenSource();
        var context = new ErgosfareContext(cancellationToken: cts.Token);

        Assert.Equal("plain", await mediator.QueryAsync<TqfPlainQuery, string>(new TqfPlainQuery(), context));

        // The nested-dispatch path's whole promise: cancellation flows from the context the
        // caller owns, so the handler must observe that token and not a default one.
        Assert.Equal(cts.Token, context.Items["tqf.token"]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task TypedQuery_WithAGroupSet_FiltersAndTreatsTheEmptySetAsNoFilter()
    {
        await using var provider = Build();
        var mediator = Facade(provider);

        // Repetition is deliberate: the canonical instance is what the executor cache
        // matches on a single reference check, so the second call takes the fast path.
        Assert.Equal("east", await mediator.QueryAsync<TqfRoutedQuery, string>(new TqfRoutedQuery(), East));
        Assert.Equal("east", await mediator.QueryAsync<TqfRoutedQuery, string>(new TqfRoutedQuery(), East));
        Assert.Equal("west", await mediator.QueryAsync<TqfRoutedQuery, string>(new TqfRoutedQuery(), West));

        Assert.Equal("plain", await mediator.QueryAsync<TqfPlainQuery, string>(new TqfPlainQuery(), GroupSet.Empty));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task TypedQuery_WithAnArray_Filters()
    {
        await using var provider = Build();
        var mediator = Facade(provider);

        Assert.Equal("west", await mediator.QueryAsync<TqfRoutedQuery, string>(new TqfRoutedQuery(), ["tqf.west"]));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task TypedQuery_WithANullGroupSet_IsRejectedAtTheCallSite()
    {
        await using var provider = Build();
        var mediator = Facade(provider);

        // Synchronously, before any dispatch: a missing filter object is a mistake in the
        // call, not a dispatch that produced nothing.
        var thrown = Assert.Throws<ArgumentNullException>(
            () => mediator.QueryAsync<TqfPlainQuery, string>(new TqfPlainQuery(), (GroupSet)null!));

        Assert.Equal("groups", thrown.ParamName);
    }
}
