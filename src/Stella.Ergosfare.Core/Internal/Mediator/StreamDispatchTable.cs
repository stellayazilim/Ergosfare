using System.Collections.Concurrent;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.DispatchRoots;
using Stella.Ergosfare.Core.Abstractions.Factories;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// One container's streaming pipelines, keyed by (query type, item type) — the streaming
/// counterpart of <see cref="PipelineExecutorCache"/> and <see cref="FrozenBroadcastTable"/>.
/// </summary>
/// <param name="dependenciesFactory">The factory the pipelines resolve participants through.</param>
/// <remarks>
/// The table belongs to its container rather than the process, so containers need no
/// guarding against each other here.
/// </remarks>
internal sealed class StreamDispatchTable(IMessageDependenciesFactory dependenciesFactory)
{
    private readonly ConcurrentDictionary<(Type QueryType, Type ResultType), object> _byPair = new();

    /// <summary>
    /// Returns the streaming pipeline of a query known by its runtime type.
    /// </summary>
    /// <typeparam name="TResult">The type of the streamed items.</typeparam>
    /// <param name="queryType">The query's runtime type.</param>
    /// <returns>The pipeline for that pair.</returns>
    /// <remarks>
    /// A generated stream root closes the generic without reflection; a pair the generator
    /// never saw falls back to reflective construction.
    /// </remarks>
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
    /// Constructs a streaming pipeline inside a generic context carrying the root's query
    /// and item types, so nothing is built reflectively.
    /// </summary>
    private sealed class DispatchVisitor : IMessageResultRootVisitor<object, IMessageDependenciesFactory>
    {
        /// <summary>
        /// The shared instance; the visitor holds no state.
        /// </summary>
        public static readonly DispatchVisitor Instance = new();

        /// <inheritdoc />
        public object Visit<TMessage, TResult>(IMessageDependenciesFactory state)
            where TMessage : IMessage
            => new StreamDispatch<TMessage, TResult>(state);
    }
}
