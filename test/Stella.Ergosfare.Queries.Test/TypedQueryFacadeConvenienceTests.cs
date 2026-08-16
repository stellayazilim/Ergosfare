using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Queries;
using Stella.Ergosfare.Queries.Abstractions;
using Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection;

namespace Stella.Ergosfare.Queries.Test;

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

    public sealed record RoutedQuery : IQuery<string>;

    [ExcludeFromDiscovery]
    [Group("tqf.east")]
    public sealed class EastRoutedHandler : IQueryHandler<RoutedQuery, string>
    {
        public ValueTask<string> HandleAsync(RoutedQuery query, ErgosfareContext context)
            => ValueTask.FromResult("east");
    }

    [ExcludeFromDiscovery]
    [Group("tqf.west")]
    public sealed class WestRoutedHandler : IQueryHandler<RoutedQuery, string>
    {
        public ValueTask<string> HandleAsync(RoutedQuery query, ErgosfareContext context)
            => ValueTask.FromResult("west");
    }

    public sealed record PlainQuery : IQuery<string>;

    [ExcludeFromDiscovery]
    public sealed class PlainQueryHandler : IQueryHandler<PlainQuery, string>
    {
        public ValueTask<string> HandleAsync(PlainQuery query, ErgosfareContext context)
        {
            context.Set("tqf.token", context.CancellationToken);
            return ValueTask.FromResult("plain");
        }
    }

    private static ServiceProvider Build()
        => new ServiceCollection()
            .AddErgosfare(x => x.AddQueryModule(q =>
            {
                q.Register<EastRoutedHandler>();
                q.Register<WestRoutedHandler>();
                q.Register<PlainQueryHandler>();
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

        Assert.Equal("plain", await mediator.QueryAsync<PlainQuery, string>(new PlainQuery(), context));

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
        Assert.Equal("east", await mediator.QueryAsync<RoutedQuery, string>(new RoutedQuery(), East));
        Assert.Equal("east", await mediator.QueryAsync<RoutedQuery, string>(new RoutedQuery(), East));
        Assert.Equal("west", await mediator.QueryAsync<RoutedQuery, string>(new RoutedQuery(), West));

        Assert.Equal("plain", await mediator.QueryAsync<PlainQuery, string>(new PlainQuery(), GroupSet.Empty));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task TypedQuery_WithAnArray_Filters()
    {
        await using var provider = Build();
        var mediator = Facade(provider);

        Assert.Equal("west", await mediator.QueryAsync<RoutedQuery, string>(new RoutedQuery(), ["tqf.west"]));
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
            () => mediator.QueryAsync<PlainQuery, string>(new PlainQuery(), (GroupSet)null!));

        Assert.Equal("groups", thrown.ParamName);
    }
}
