using System.Collections.Concurrent;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.DispatchRoots;
using Stella.Ergosfare.Core.Abstractions.Factories;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// One container's table of streaming pipelines, keyed by (query type, result type) — the
/// streaming counterpart of the executor cache and the broadcast table, and the same shape:
/// a table the container owns rather than a process-wide store every container is guarded
/// against.
/// </summary>
internal sealed class StreamDispatchTable(IMessageDependenciesFactory dependenciesFactory)
{
    private readonly ConcurrentDictionary<(Type QueryType, Type ResultType), object> _byPair = new();

    /// <summary>
    /// The pipeline for a query known by its runtime type. The generated stream root closes
    /// the generic without reflection; a pair the generator never saw falls to the reflective
    /// construction inside <see cref="DispatchLookup"/>.
    /// </summary>
    internal IStreamDispatch<TResult> Get<TResult>(Type queryType)
    {
        var key = (queryType, typeof(TResult));

        if (_byPair.TryGetValue(key, out var dispatch))
        {
            return (IStreamDispatch<TResult>)dispatch;
        }

        return (IStreamDispatch<TResult>)_byPair.GetOrAdd(
            key,
            DispatchLookup.OverStream(
                queryType, typeof(TResult), DispatchVisitor.Instance, dependenciesFactory,
                typeof(StreamDispatch<,>), [dependenciesFactory]));
    }

    /// <summary>
    /// Re-enters a generic context with a root's (query, result) pair and constructs the
    /// closed pipeline there — no <see cref="Type.MakeGenericType"/>, no reflection.
    /// </summary>
    private sealed class DispatchVisitor : IMessageResultRootVisitor<object, IMessageDependenciesFactory>
    {
        public static readonly DispatchVisitor Instance = new();

        public object Visit<TMessage, TResult>(IMessageDependenciesFactory state)
            where TMessage : IMessage
            => new StreamDispatch<TMessage, TResult>(state);
    }
}
