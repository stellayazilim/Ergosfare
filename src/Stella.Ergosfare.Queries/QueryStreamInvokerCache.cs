using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.DispatchRoots;

namespace Stella.Ergosfare.Queries;

/// <summary>
/// Process-wide cache of <see cref="IQueryStreamInvoker{TResult}"/> instances, one per
/// (query runtime type, result type) — one <see cref="Type.MakeGenericType"/> per pair.
/// </summary>
internal static class QueryStreamInvokerCache
{
    private static readonly ConcurrentDictionary<(Type QueryType, Type ResultType), object> Invokers = new();

    public static IQueryStreamInvoker<TResult> Get<TResult>(Type queryType)
    {
        var key = (queryType, typeof(TResult));

        if (!Invokers.TryGetValue(key, out var invoker))
        {
            invoker = Invokers.GetOrAdd(
                key,
                DispatchLookup.OverStream(
                    queryType, typeof(TResult), InvokerVisitor.Instance, state: false,
                    typeof(QueryStreamInvoker<,>)));
        }

        return (IQueryStreamInvoker<TResult>)invoker;
    }

    /// <summary>
    /// Re-enters a generic context with a root's (query, result) pair and constructs the
    /// closed stream invoker there — no <see cref="Type.MakeGenericType"/>, no reflection.
    /// </summary>
    private sealed class InvokerVisitor : IMessageResultRootVisitor<object, bool>
    {
        public static readonly InvokerVisitor Instance = new();

        public object Visit<TMessage, TResult>(bool state) where TMessage : IMessage
            => new QueryStreamInvoker<TMessage, TResult>();
    }
}
