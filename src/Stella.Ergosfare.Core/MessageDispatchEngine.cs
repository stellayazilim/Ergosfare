using System.Runtime.CompilerServices;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Abstractions.Factories;
using Stella.Ergosfare.Core.Internal.Mediator;

namespace Stella.Ergosfare.Core;

/// <summary>
/// The scope-free dispatch engine behind every mediator facade: the pipeline-executor
/// lookup, pooled execution context, and completion handling of the
/// <see cref="IMessageMediator"/> executor path, with the calling scope's provider supplied
/// per call instead of being captured per instance. One process-wide singleton serves every
/// scope, so resolving an engine-backed facade builds exactly one object per resolution.
/// </summary>
/// <remarks>
/// Construction is internal: the engine only makes sense wired to the process-wide
/// <see cref="PipelineExecutorCache"/> and dependencies factory that the DI registration
/// supplies. Dispatch behavior matches <see cref="IMessageMediator"/>'s executor overloads
/// exactly — the mediator delegates here, passing its own captured provider.
/// </remarks>
public sealed class MessageDispatchEngine
{
    /// <summary>
    /// Process-wide executor cache; one closed executor per message (and result) type.
    /// </summary>
    private readonly PipelineExecutorCache _executorCache;

    /// <summary>
    /// This container's broadcast pipelines, one per message type — the publishing
    /// counterpart of the executor cache above.
    /// </summary>
    private readonly BroadcastDispatchTable _broadcasts;

    /// <summary>
    /// This container's streaming pipelines, one per (query, result) pair.
    /// </summary>
    private readonly StreamDispatchTable _streams;

    /// <summary>
    /// The dependencies factory the executors build their pipeline plans against.
    /// </summary>
    private readonly IMessageDependenciesFactory _dependenciesFactory;

    internal MessageDispatchEngine(PipelineExecutorCache executorCache, IMessageDependenciesFactory dependenciesFactory)
    {
        _executorCache = executorCache ?? throw new ArgumentNullException(nameof(executorCache));
        _dependenciesFactory = dependenciesFactory ?? throw new ArgumentNullException(nameof(dependenciesFactory));
        _broadcasts = new BroadcastDispatchTable(_dependenciesFactory);
        _streams = new StreamDispatchTable(_dependenciesFactory);
    }

    /// <inheritdoc cref="_dependenciesFactory"/>
    internal IMessageDependenciesFactory DependenciesFactory => _dependenciesFactory;

    /// <summary>
    /// Broadcasts a message to every handler of its pipeline, renting a pooled context for
    /// the delivery. The publishing counterpart of <see cref="DispatchAsync(object,IServiceProvider,IDictionary{object,object?},CancellationToken,IEnumerable{string})"/>,
    /// and the same shape: find this container's pipeline for the type, run it, return the
    /// context inline when the delivery completed synchronously.
    /// </summary>
    /// <param name="message">The message to broadcast.</param>
    /// <param name="serviceProvider">The scope provider handlers resolve against.</param>
    /// <param name="cancellationToken">Cancellation token for the delivery.</param>
    /// <param name="groups">Optional group filters applied to the pipeline.</param>
    /// <param name="throwIfNoHandlerFound">Whether reaching nobody is an error.</param>
    public ValueTask BroadcastAsync(object message, IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default,
        IEnumerable<string>? groups = null, bool throwIfNoHandlerFound = false)
    {
        ArgumentNullException.ThrowIfNull(message);

        return Rent(
            _broadcasts.Get(message.GetType()), message, serviceProvider, cancellationToken,
            groups, throwIfNoHandlerFound);
    }

    /// <summary>
    /// Typed broadcast: when the compile-time <typeparamref name="TMessage"/> is the message's
    /// runtime type (the overwhelmingly common concrete-typed publish), the pipeline comes
    /// from a static-generic slot instead of the type-keyed dictionary. A base-typed generic
    /// call falls back to resolving by the runtime type.
    /// </summary>
    public ValueTask BroadcastAsync<TMessage>(TMessage message, IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default,
        IEnumerable<string>? groups = null, bool throwIfNoHandlerFound = false)
        where TMessage : notnull
    {
        ArgumentNullException.ThrowIfNull(message);

        var dispatch = message.GetType() == typeof(TMessage)
            ? _broadcasts.Get<TMessage>()
            : _broadcasts.Get(message.GetType());

        return Rent(dispatch, message, serviceProvider, cancellationToken, groups, throwIfNoHandlerFound);
    }

    /// <summary>
    /// Broadcasts under an externally owned context — the nested-publish path. The caller owns
    /// the context's lifetime, so nothing is rented and nothing is returned.
    /// </summary>
    public ValueTask BroadcastAsync(object message, ErgosfareContext context, IServiceProvider serviceProvider,
        IEnumerable<string>? groups = null, bool throwIfNoHandlerFound = false)
    {
        ArgumentNullException.ThrowIfNull(message);

        return _broadcasts.Get(message.GetType())
            .Publish(message, context, serviceProvider, groups, throwIfNoHandlerFound);
    }

    /// <summary>
    /// Streams a query through this container's pipeline for it. The context is fresh and
    /// unpooled: enumeration happens after this call returns, so its completion is not
    /// observable here and the context cannot go back to the pool.
    /// </summary>
    public IAsyncEnumerable<TResult> StreamAsync<TResult>(object query, IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default,
        IEnumerable<string>? groups = null)
    {
        ArgumentNullException.ThrowIfNull(query);

        return _streams.Get<TResult>(query.GetType())
            .Stream(query, null, cancellationToken, serviceProvider, groups);
    }

    /// <summary>
    /// Streams under a caller-owned context — the shape that lets a caller read back what the
    /// pipeline wrote. A streaming context is never pooled either way.
    /// </summary>
    public IAsyncEnumerable<TResult> StreamAsync<TResult>(object query, ErgosfareContext context,
        IServiceProvider serviceProvider, IEnumerable<string>? groups = null)
    {
        ArgumentNullException.ThrowIfNull(query);

        return _streams.Get<TResult>(query.GetType())
            .Stream(query, context, context.CancellationToken, serviceProvider, groups);
    }

    private static ValueTask Rent(
        BroadcastDispatch dispatch, object message, IServiceProvider serviceProvider,
        CancellationToken cancellationToken,
        IEnumerable<string>? groups, bool throwIfNoHandlerFound)
    {
        var context = ErgosfareContextPool.Rent(null, cancellationToken);
        ValueTask task;

        try
        {
            task = dispatch.Publish(message, context, serviceProvider, groups, throwIfNoHandlerFound);
        }
        catch
        {
            ErgosfareContextPool.Return(context);
            throw;
        }

        if (task.IsCompletedSuccessfully)
        {
            ErgosfareContextPool.Return(context);
            return default;
        }

        return AwaitAndReturn(task, context);

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
        static async ValueTask AwaitAndReturn(ValueTask task, ErgosfareContext context)
        {
            try
            {
                await task;
            }
            finally
            {
                ErgosfareContextPool.Return(context);
            }
        }
    }

    /// <summary>
    /// Dispatches a void message through its cached pipeline executor, resolving handlers
    /// against <paramref name="serviceProvider"/> — the caller's scope.
    /// </summary>
    /// <param name="message">The message to dispatch.</param>
    /// <param name="serviceProvider">The scope provider handlers resolve against.</param>
    /// <param name="cancellationToken">Cancellation token for the dispatch.</param>
    /// <param name="groups">Optional group filters applied to the pipeline.</param>
    public ValueTask DispatchAsync(object message, IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default,
        IEnumerable<string>? groups = null)
    {
        ArgumentNullException.ThrowIfNull(message);

        var executor = _executorCache.GetVoidExecutor(message.GetType());
        var context = ErgosfareContextPool.Rent(null, cancellationToken);
        ValueTask task;

        try
        {
            task = executor.Execute(message, context, serviceProvider, groups);
        }
        catch
        {
            ErgosfareContextPool.Return(context);
            throw;
        }

        // Synchronously completed dispatches (the common case) return the context inline —
        // no async state machine on the hot path. Only a genuinely suspended pipeline pays
        // for the awaiting helper.
        if (task.IsCompletedSuccessfully)
        {
            ErgosfareContextPool.Return(context);
            return default;
        }

        return AwaitAndReturn(task, context);

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
        static async ValueTask AwaitAndReturn(ValueTask task, ErgosfareContext context)
        {
            try
            {
                await task;
            }
            finally
            {
                ErgosfareContextPool.Return(context);
            }
        }
    }

    /// <summary>
    /// Typed void dispatch: when the compile-time <typeparamref name="TMessage"/> is the
    /// message's runtime type (the overwhelmingly common concrete-typed call), the
    /// executor comes from a static-generic holder instead of the type-keyed dictionary —
    /// the last lookup on the group-less hot path. A base-typed generic call falls back to
    /// resolving by the runtime type, so dispatch semantics are identical to
    /// <see cref="DispatchAsync(object, IServiceProvider, IDictionary{object, object?}?, CancellationToken, IEnumerable{string}?)"/>;
    /// group-filtered dispatches stay on that overload.
    /// </summary>
    /// <remarks>
    /// Deliberately NOT a <c>DispatchAsync</c> overload: a same-name generic would join
    /// the candidate set of every explicit <c>DispatchAsync&lt;T&gt;(msg, ...)</c> call, and
    /// whenever the message expression is convertible to the type argument (an echo-typed
    /// result dispatch — legal since <c>ICommand&lt;TResult&gt; : ICommand : IMessage</c>) the
    /// identity conversion would out-rank the result overload's <c>object</c> parameter,
    /// silently rerouting a result dispatch through the void pipeline or breaking the
    /// caller with a return-type mismatch. The distinct name keeps the typed fast path
    /// out of that candidate set entirely.
    /// </remarks>
    /// <typeparam name="TMessage">The compile-time message type.</typeparam>
    /// <param name="message">The message to dispatch.</param>
    /// <param name="serviceProvider">The scope provider handlers resolve against.</param>
    /// <param name="cancellationToken">Cancellation token for the dispatch.</param>
    public ValueTask DispatchVoidAsync<TMessage>(TMessage message, IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
        where TMessage : IMessage
    {
        ArgumentNullException.ThrowIfNull(message);

        var executor = message.GetType() == typeof(TMessage)
            ? _executorCache.GetVoidExecutor<TMessage>()
            : _executorCache.GetVoidExecutor(message.GetType());
        var context = ErgosfareContextPool.Rent(null, cancellationToken);
        ValueTask task;

        try
        {
            task = executor.Execute(message, context, serviceProvider, null);
        }
        catch
        {
            ErgosfareContextPool.Return(context);
            throw;
        }

        if (task.IsCompletedSuccessfully)
        {
            ErgosfareContextPool.Return(context);
            return default;
        }

        return AwaitAndReturn(task, context);

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
        static async ValueTask AwaitAndReturn(ValueTask task, ErgosfareContext context)
        {
            try
            {
                await task;
            }
            finally
            {
                ErgosfareContextPool.Return(context);
            }
        }
    }

    /// <summary>
    /// Result-producing counterpart of
    /// <see cref="DispatchAsync(object, IServiceProvider, IDictionary{object, object?}?, CancellationToken, IEnumerable{string}?)"/>.
    /// </summary>
    /// <typeparam name="TResult">The expected result type of the message.</typeparam>
    public ValueTask<TResult> DispatchAsync<TResult>(object message, IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default,
        IEnumerable<string>? groups = null)
    {
        ArgumentNullException.ThrowIfNull(message);

        var executor = _executorCache.GetExecutor<TResult>(message.GetType());
        var context = ErgosfareContextPool.Rent(null, cancellationToken);
        ValueTask<TResult> task;

        try
        {
            task = executor.Execute(message, context, serviceProvider, groups);
        }
        catch
        {
            ErgosfareContextPool.Return(context);
            throw;
        }

        if (task.IsCompletedSuccessfully)
        {
            var result = task.Result;
            ErgosfareContextPool.Return(context);
            return new ValueTask<TResult>(result);
        }

        return AwaitAndReturn(task, context);

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
        static async ValueTask<TResult> AwaitAndReturn(ValueTask<TResult> task, ErgosfareContext context)
        {
            try
            {
                return await task;
            }
            finally
            {
                ErgosfareContextPool.Return(context);
            }
        }
    }

    /// <summary>
    /// Dispatches a void message under a caller-owned execution context (typically a
    /// scope's child): the caller controls the context's lifetime, so nothing is rented or
    /// returned here.
    /// </summary>
    /// <param name="message">The message to dispatch.</param>
    /// <param name="context">The externally owned execution context.</param>
    /// <param name="serviceProvider">The scope provider handlers resolve against.</param>
    /// <param name="groups">Optional group filters applied to the pipeline.</param>
    /// <summary>
    /// Typed counterpart of the context dispatch: the executor comes from the static-generic
    /// slot when <typeparamref name="TMessage"/> is the message's runtime type. The caller owns
    /// the context, so nothing is rented and nothing is returned.
    /// </summary>
    public ValueTask DispatchVoidAsync<TMessage>(TMessage message, ErgosfareContext context,
        IServiceProvider serviceProvider, IEnumerable<string>? groups = null)
        where TMessage : IMessage
    {
        ArgumentNullException.ThrowIfNull(message);

        // The filter no longer selects the executor, so the typed slot serves a filtered
        // dispatch too — only the runtime-type guard stands between a call and the field read.
        var executor = message.GetType() == typeof(TMessage)
            ? _executorCache.GetVoidExecutor<TMessage>()
            : _executorCache.GetVoidExecutor(message.GetType());

        return executor.Execute(message, context, serviceProvider, groups);
    }

    public ValueTask DispatchAsync(object message, ErgosfareContext context, IServiceProvider serviceProvider,
        IEnumerable<string>? groups = null)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(context);

        var executor = _executorCache.GetVoidExecutor(message.GetType());

        return executor.Execute(message, context, serviceProvider, groups);
    }

    /// <summary>
    /// Result-producing counterpart of
    /// <see cref="DispatchAsync(object, ErgosfareContext, IServiceProvider, IEnumerable{string}?)"/>.
    /// </summary>
    /// <typeparam name="TResult">The expected result type of the message.</typeparam>
    public ValueTask<TResult> DispatchAsync<TResult>(object message, ErgosfareContext context, IServiceProvider serviceProvider,
        IEnumerable<string>? groups = null)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(context);

        var executor = _executorCache.GetExecutor<TResult>(message.GetType());

        return executor.Execute(message, context, serviceProvider, groups);
    }
}
