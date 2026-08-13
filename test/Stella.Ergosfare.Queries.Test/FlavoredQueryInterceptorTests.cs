using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Queries.Abstractions;
using Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Queries.Test;

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
    [ExcludeFromDiscovery]
    public sealed class FinalizedIntQuery : IQuery<int> { }

    [ExcludeFromDiscovery]
    public sealed class FinalizedIntQueryHandler : IQueryHandler<FinalizedIntQuery, int>
    {
        public ValueTask<int> HandleAsync(FinalizedIntQuery query, ErgosfareContext context)
            => ValueTask.FromResult(7);
    }

    [ExcludeFromDiscovery]
    public sealed class RecordingQueryFinalInterceptor : IQueryFinalInterceptor
    {
        public ValueTask HandleAsync(IQuery query, object? messageResult, Exception? exception, ErgosfareContext context)
        {
            context.Set("finalRan", true);
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ValueTypedResult_RunsTheNonGenericFinalInterceptor()
    {
        var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddQueryModule(q =>
            {
                q.Register<FinalizedIntQueryHandler>();
                q.Register<RecordingQueryFinalInterceptor>();
            }))
            .BuildServiceProvider();
        await using var _ = provider;

        var settings = new Dictionary<object, object?>();

        // Before the contract fix the final stage itself threw NotSupportedException for
        // value-typed results; now it observes the outcome like any final interceptor.
        var result = await provider.GetRequiredService<IQueryMediator>().QueryAsync(new FinalizedIntQuery(), settings);

        Assert.Equal(7, result);
        Assert.Equal(true, settings["finalRan"]);
    }

    [ExcludeFromDiscovery]
    public sealed class TargetedQuery : IQuery<int> { }

    [ExcludeFromDiscovery]
    public sealed class TargetedQueryHandler : IQueryHandler<TargetedQuery, int>
    {
        public ValueTask<int> HandleAsync(TargetedQuery query, ErgosfareContext context)
            => ValueTask.FromResult(1);
    }

    [ExcludeFromDiscovery]
    public sealed class UntargetedQuery : IQuery<int> { }

    [ExcludeFromDiscovery]
    public sealed class UntargetedQueryHandler : IQueryHandler<UntargetedQuery, int>
    {
        public ValueTask<int> HandleAsync(UntargetedQuery query, ErgosfareContext context)
            => ValueTask.FromResult(2);
    }

    [ExcludeFromDiscovery]
    public sealed class TargetedQueryPostInterceptor : IQueryPostInterceptor<TargetedQuery>
    {
        public ValueTask<object> HandleAsync(TargetedQuery query, object messageResult, ErgosfareContext context)
        {
            context.Set("postRan", true);
            return ValueTask.FromResult(messageResult);
        }
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
        var untargetedSettings = new Dictionary<object, object?>();
        await queries.QueryAsync(new UntargetedQuery(), untargetedSettings);
        Assert.False(untargetedSettings.ContainsKey("postRan"));

        var targetedSettings = new Dictionary<object, object?>();
        await queries.QueryAsync(new TargetedQuery(), targetedSettings);
        Assert.Equal(true, targetedSettings["postRan"]);
    }
}
