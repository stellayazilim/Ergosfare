using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Stella.Ergosfare.Core.Abstractions;

namespace Stella.Ergosfare.Queries;

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
        Justification = "Generated dispatch roots cover source-generated query types; this reflective path is the " +
                        "JIT fallback for runtime-only registrations.")]
    public static IQueryStreamInvoker<TResult> Get<TResult>(Type queryType)
    {
        var key = (queryType, typeof(TResult));

        if (!Invokers.TryGetValue(key, out var invoker))
        {
            // Generated dispatch roots close the invoker generic at compile time; the
            // reflective path below only serves query types without a root.
            invoker = GeneratedDispatchRoots.FindStream(queryType, typeof(TResult)) is { } root
                ? Invokers.GetOrAdd(key, root.Accept(InvokerVisitor.Instance, state: false))
                : Invokers.GetOrAdd(key,
                    static k => Activator.CreateInstance(typeof(QueryStreamInvoker<,>).MakeGenericType(k.QueryType, k.ResultType))!);
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
