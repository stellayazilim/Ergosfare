using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Stella.Ergosfare.Core;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Strategies;
using Stella.Ergosfare.Queries.Abstractions;

namespace Stella.Ergosfare.Queries;

/// <summary>
/// Streams a query through a pipeline closed over the query's concrete type, so the stream
/// handler is always invoked through its typed member — interface-erased streaming
/// (<c>StreamAsync(IStreamQuery&lt;T&gt;)</c>) resolves the invoker from the query's runtime
/// type. Invokers are closed once per (query type, result type) and cached; the per-call
/// cancellation token flows into a fresh strategy instance, as before.
/// </summary>
internal interface IQueryStreamInvoker<out TResult>
{
    IAsyncEnumerable<TResult> Stream(object query, QueryMediationSettings? settings, CancellationToken cancellationToken,
        IMessageMediator mediator, ActualTypeOrFirstAssignableTypeMessageResolveStrategy resolveStrategy);
}

internal sealed class QueryStreamInvoker<TQuery, TResult> : IQueryStreamInvoker<TResult>
    where TQuery : notnull
{
    private static readonly string[] EmptyGroups = [];

    public IAsyncEnumerable<TResult> Stream(object query, QueryMediationSettings? settings, CancellationToken cancellationToken,
        IMessageMediator mediator, ActualTypeOrFirstAssignableTypeMessageResolveStrategy resolveStrategy)
    {
        var options = new MediateOptions<TQuery, IAsyncEnumerable<TResult>>
        {
            MessageMediationStrategy = new SingleStreamHandlerMediationStrategy<TQuery, TResult>(new ResultAdapterService(), cancellationToken),
            MessageResolveStrategy = resolveStrategy,
            CancellationToken = cancellationToken,
            Items = settings?.Items,
            Groups = settings?.Filters.Groups ?? EmptyGroups,
        };

        return mediator.Mediate((TQuery)query, options);
    }
}

/// <summary>
/// Process-wide cache of <see cref="IQueryStreamInvoker{TResult}"/> instances, one per
/// (query runtime type, result type) — one <see cref="Type.MakeGenericType"/> per pair.
/// </summary>
internal static class QueryStreamInvokerCache
{
    private static readonly ConcurrentDictionary<(Type QueryType, Type ResultType), object> Invokers = new();

    [UnconditionalSuppressMessage("Trimming", "IL2055",
        Justification = "The invoker generic is closed over a live query's runtime type; the query roots its type.")]
    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "Invokers close over the query's reference type; the struct result argument's instantiations are " +
                        "rooted by source-generated dispatch on Native AOT, with this reflective path as the JIT fallback.")]
    public static IQueryStreamInvoker<TResult> Get<TResult>(Type queryType)
    {
        var key = (queryType, typeof(TResult));
        if (!Invokers.TryGetValue(key, out var invoker))
        {
            invoker = Invokers.GetOrAdd(key,
                static k => Activator.CreateInstance(typeof(QueryStreamInvoker<,>).MakeGenericType(k.QueryType, k.ResultType))!);
        }

        return (IQueryStreamInvoker<TResult>)invoker;
    }
}
