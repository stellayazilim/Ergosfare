using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Queries.Abstractions;
using Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Queries.Test;

// The fixtures live at the top level so the source generator compiles their pipelines;
// every type is owned by FlavoredQueryInterceptorTests alone, and each container below
// registers its query's full compiled pipeline. The final interceptor is the typed,
// query-scoped contract — the non-generic IQueryFinalInterceptor declares IQuery itself,
// and a module-marker-wide participant would enter every query's compiled composition in
// the assembly.

public sealed class FinalizedIntQuery : IQuery<int> { }

public sealed class FinalizedIntQueryHandler : IQueryHandler<FinalizedIntQuery, int>
{
    public ValueTask<int> HandleAsync(FinalizedIntQuery query, ErgosfareContext context)
        => ValueTask.FromResult(7);
}

public sealed class FinalizedIntQueryFinalInterceptor : IQueryFinalInterceptor<FinalizedIntQuery, int>
{
    public ValueTask HandleAsync(FinalizedIntQuery query, int result, Exception? exception, ErgosfareContext context)
    {
        context.Set("finalRan", true);
        return ValueTask.CompletedTask;
    }
}

public sealed class TargetedQuery : IQuery<int> { }

public sealed class TargetedQueryHandler : IQueryHandler<TargetedQuery, int>
{
    public ValueTask<int> HandleAsync(TargetedQuery query, ErgosfareContext context)
        => ValueTask.FromResult(1);
}

public sealed class UntargetedQuery : IQuery<int> { }

public sealed class UntargetedQueryHandler : IQueryHandler<UntargetedQuery, int>
{
    public ValueTask<int> HandleAsync(UntargetedQuery query, ErgosfareContext context)
        => ValueTask.FromResult(2);
}

public sealed class TargetedQueryPostInterceptor : IQueryPostInterceptor<TargetedQuery>
{
    public ValueTask<object> HandleAsync(TargetedQuery query, object messageResult, ErgosfareContext context)
    {
        context.Set("postRan", true);
        return ValueTask.FromResult(messageResult);
    }
}

/// <summary>
/// Regression coverage for two flavored query interceptor contracts. The non-generic
/// <see cref="IQueryFinalInterceptor"/> used to extend the result-typed
/// <c>IAsyncFinalInterceptor&lt;IQuery, object&gt;</c>, which no invocation-strategy arm
/// can match when the query result is a value type — the final stage itself threw
/// <see cref="NotSupportedException"/> for every <c>IQuery&lt;int&gt;</c>-style query.
/// And <see cref="IQueryPostInterceptor{TQuery}"/> used to close its base over
/// <see cref="IQuery"/> instead of <c>TQuery</c>, silently applying the interceptor to
/// every query in the application instead of the targeted one.
/// </summary>
public class FlavoredQueryInterceptorTests
{
    /// <summary>
    /// A probe for the non-generic contract, invoked directly by the test below and never
    /// dispatched — a discoverable <see cref="IQueryFinalInterceptor"/> would enter every
    /// query's compiled composition in the assembly.
    /// </summary>
    [ExcludeFromDiscovery]
    private sealed class RecordingQueryFinalInterceptor : IQueryFinalInterceptor
    {
        public object? SeenResult;

        public ValueTask HandleAsync(IQuery query, object? messageResult, Exception? exception, ErgosfareContext context)
        {
            SeenResult = messageResult;
            return ValueTask.CompletedTask;
        }
    }

    [ExcludeFromDiscovery]
    private sealed record ContractProbeQuery : IQuery<int>;

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ValueTypedResult_RunsAFinalInterceptor()
    {
        var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddQueryModule(q =>
            {
                q.Register<FinalizedIntQueryHandler>();
                q.Register<FinalizedIntQueryFinalInterceptor>();
            }))
            .BuildServiceProvider();
        await using var _ = provider;

        var settings = new ErgosfareContext();

        // Before the contract fix the final stage itself threw NotSupportedException for
        // value-typed results; now it observes the outcome like any final interceptor —
        // here through the compiled plan that bakes it.
        var result = await provider.GetRequiredService<IQueryMediator>().QueryAsync(new FinalizedIntQuery(), settings);

        Assert.Equal(7, result);
        Assert.Equal(true, settings.Items["finalRan"]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task NonGenericFinalInterceptor_ReceivesAValueTypedResult_ThroughTheResultAgnosticBase()
    {
        // The regression was the contract's base: IAsyncFinalInterceptor<IQuery, object>
        // has no arm a value-typed result can match, while the result-agnostic
        // IAsyncFinalInterceptor<IQuery> receives any result boxed. This cast compiles
        // only against the fixed base, and the boxed int must arrive intact.
        var interceptor = new RecordingQueryFinalInterceptor();

        await ((IAsyncFinalInterceptor<IQuery>)interceptor)
            .HandleAsync(new ContractProbeQuery(), 7, null, null!);

        Assert.Equal(7, interceptor.SeenResult);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task TypedPostInterceptor_AppliesOnlyToItsTargetQuery()
    {
        var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddQueryModule(q =>
            {
                q.Register<TargetedQueryHandler>();
                q.Register<UntargetedQueryHandler>();
                q.Register<TargetedQueryPostInterceptor>();
            }))
            .BuildServiceProvider();
        await using var _ = provider;

        var queries = provider.GetRequiredService<IQueryMediator>();

        // Before the contract fix the interceptor's base closed over IQuery, so it ran
        // for every query in the application — including this untargeted one.
        var untargetedSettings = new ErgosfareContext();
        await queries.QueryAsync(new UntargetedQuery(), untargetedSettings);
        Assert.False(untargetedSettings.Items.ContainsKey("postRan"));

        var targetedSettings = new ErgosfareContext();
        await queries.QueryAsync(new TargetedQuery(), targetedSettings);
        Assert.Equal(true, targetedSettings.Items["postRan"]);
    }
}
