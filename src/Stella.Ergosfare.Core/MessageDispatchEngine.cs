using System.Runtime.CompilerServices;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Factories;
using Stella.Ergosfare.Core.Internal.Mediator;

namespace Stella.Ergosfare.Core;

/// <summary>
/// The dispatch machinery behind every mediator: it finds the pipeline for a message, runs
/// it under an execution context, and hands back the result.
/// </summary>
/// <remarks>
/// The engine holds no scope of its own — the calling scope's provider is passed in per
/// call — so one instance serves the whole process and resolving a mediator builds a single
/// object. Its constructor is internal because the engine only makes sense wired to the
/// executor cache and dependencies factory that registration supplies.
/// </remarks>
public sealed class MessageDispatchEngine
{
    /// <summary>
    /// The pipeline executors, one per message type and per (message, result) pair.
    /// </summary>
    private readonly PipelineExecutorCache _executorCache;

    /// <summary>
    /// This container's publish pipelines, one per event type.
    /// </summary>
    private readonly FrozenBroadcastTable _broadcasts;

    /// <summary>
    /// This container's streaming pipelines, one per (query, result) pair.
    /// </summary>
    private readonly StreamDispatchTable _streams;

    /// <summary>
    /// The factory the pipelines resolve their participants through.
    /// </summary>
    private readonly IMessageDependenciesFactory _dependenciesFactory;

    /// <summary>
    /// Initializes the engine over a container's executor cache and dependencies factory.
    /// </summary>
    /// <param name="executorCache">The container's pipeline executors.</param>
    /// <param name="dependenciesFactory">The container's dependencies factory.</param>
    /// <exception cref="ArgumentNullException">Either argument is <c>null</c>.</exception>
    internal MessageDispatchEngine(PipelineExecutorCache executorCache, IMessageDependenciesFactory dependenciesFactory)
    {
        _executorCache = executorCache ?? throw new ArgumentNullException(nameof(executorCache));
        _dependenciesFactory = dependenciesFactory ?? throw new ArgumentNullException(nameof(dependenciesFactory));
        _broadcasts = new FrozenBroadcastTable(_dependenciesFactory);
        _streams = new StreamDispatchTable(_dependenciesFactory);
    }

    /// <inheritdoc cref="_dependenciesFactory"/>
    internal IMessageDependenciesFactory DependenciesFactory => _dependenciesFactory;

    /// <inheritdoc cref="_broadcasts"/>
    /// <remarks>
    /// Exposed to the event facade so a typed publish is one call into the pipeline, with
    /// no relay frames in between.
    /// </remarks>
    internal FrozenBroadcastTable Broadcasts => _broadcasts;

    /// <summary>
    /// Publishes <paramref name="message"/> to every handler of its pipeline.
    /// </summary>
    /// <param name="message">The event to publish. Cannot be <c>null</c>.</param>
    /// <param name="serviceProvider">The provider handlers are resolved against.</param>
    /// <param name="cancellationToken">Token for the delivery.</param>
    /// <param name="groups">The groups to deliver to; <c>null</c> uses the default group.</param>
    /// <returns>A task that completes when every handler has run.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <c>null</c>.</exception>
    /// <remarks>
    /// The execution context is created and released here, so a delivery that completes
    /// synchronously releases it without an async continuation.
    /// </remarks>
    public ValueTask BroadcastAsync(object message, IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default,
        IEnumerable<string>? groups = null)
    {
        ArgumentNullException.ThrowIfNull(message);

        return _broadcasts.Get(message.GetType())
            .PublishPooled(message, serviceProvider, cancellationToken, groups);
    }

    /// <summary>
    /// Publishes <paramref name="message"/>, naming its type at compile time.
    /// </summary>
    /// <typeparam name="TMessage">The event's compile-time type.</typeparam>
    /// <param name="message">The event to publish. Cannot be <c>null</c>.</param>
    /// <param name="serviceProvider">The provider handlers are resolved against.</param>
    /// <param name="cancellationToken">Token for the delivery.</param>
    /// <param name="groups">The groups to deliver to; <c>null</c> uses the default group.</param>
    /// <returns>A task that completes when every handler has run.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <c>null</c>.</exception>
    /// <remarks>
    /// When <typeparamref name="TMessage"/> is the event's runtime type — the usual case —
    /// the pipeline comes from a static generic slot instead of a type-keyed lookup.
    /// Publishing through a base type falls back to looking the runtime type up, so
    /// delivery is the same either way.
    /// </remarks>
    public ValueTask BroadcastAsync<TMessage>(TMessage message, IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default,
        IEnumerable<string>? groups = null)
        where TMessage : notnull
    {
        ArgumentNullException.ThrowIfNull(message);

        var dispatch = message.GetType() == typeof(TMessage)
            ? _broadcasts.Get<TMessage>()
            : _broadcasts.Get(message.GetType());

        // The context is created and released inside the dispatch's own frame, so this
        // method adds none of its own.
        return dispatch.PublishPooled(message, serviceProvider, cancellationToken, groups);
    }

    /// <summary>
    /// Publishes <paramref name="message"/> under an execution context the caller owns —
    /// the shape a nested publish uses.
    /// </summary>
    /// <param name="message">The event to publish. Cannot be <c>null</c>.</param>
    /// <param name="context">The caller's execution context.</param>
    /// <param name="serviceProvider">The provider handlers are resolved against.</param>
    /// <param name="groups">The groups to deliver to; <c>null</c> uses the default group.</param>
    /// <returns>A task that completes when every handler has run.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <c>null</c>.</exception>
    public ValueTask BroadcastAsync(object message, ErgosfareContext context, IServiceProvider serviceProvider,
        IEnumerable<string>? groups = null)
    {
        ArgumentNullException.ThrowIfNull(message);

        return _broadcasts.Get(message.GetType())
            .Publish(message, context, serviceProvider, groups);
    }

    /// <summary>
    /// Streams the results of <paramref name="query"/> through its pipeline.
    /// </summary>
    /// <typeparam name="TResult">The type of the streamed items.</typeparam>
    /// <param name="query">The query to stream. Cannot be <c>null</c>.</param>
    /// <param name="serviceProvider">The provider handlers are resolved against.</param>
    /// <param name="cancellationToken">Token for the enumeration.</param>
    /// <param name="groups">The groups to run; <c>null</c> uses the default group.</param>
    /// <returns>The streamed results.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <c>null</c>.</exception>
    /// <remarks>
    /// The context for a stream is created fresh and never released back: enumeration
    /// happens after this method returns, so there is no point at which the context is
    /// known to be finished with.
    /// </remarks>
    public IAsyncEnumerable<TResult> StreamAsync<TResult>(object query, IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default,
        IEnumerable<string>? groups = null)
    {
        ArgumentNullException.ThrowIfNull(query);

        return _streams.Get<TResult>(query.GetType())
            .Stream(query, null, cancellationToken, serviceProvider, groups);
    }

    /// <summary>
    /// Streams the results of <paramref name="query"/> under an execution context the
    /// caller owns, so the caller can read back what the pipeline recorded.
    /// </summary>
    /// <typeparam name="TResult">The type of the streamed items.</typeparam>
    /// <param name="query">The query to stream. Cannot be <c>null</c>.</param>
    /// <param name="context">The caller's execution context, whose token is used.</param>
    /// <param name="serviceProvider">The provider handlers are resolved against.</param>
    /// <param name="groups">The groups to run; <c>null</c> uses the default group.</param>
    /// <returns>The streamed results.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <c>null</c>.</exception>
    public IAsyncEnumerable<TResult> StreamAsync<TResult>(object query, ErgosfareContext context,
        IServiceProvider serviceProvider, IEnumerable<string>? groups = null)
    {
        ArgumentNullException.ThrowIfNull(query);

        return _streams.Get<TResult>(query.GetType())
            .Stream(query, context, context.CancellationToken, serviceProvider, groups);
    }

    /// <summary>
    /// Dispatches <paramref name="message"/> through its void pipeline.
    /// </summary>
    /// <param name="message">The message to dispatch. Cannot be <c>null</c>.</param>
    /// <param name="serviceProvider">The provider participants are resolved against.</param>
    /// <param name="cancellationToken">Token for the dispatch.</param>
    /// <param name="groups">The groups to run; <c>null</c> uses the default group.</param>
    /// <returns>A task that completes when the pipeline has run.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <c>null</c>.</exception>
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

        // A pipeline that finished synchronously — the common case — releases its context
        // here, so no async state machine is built for it. Only a pipeline that actually
        // suspended pays for the helper below.
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
    /// Dispatches <paramref name="message"/> through its void pipeline, naming its type at
    /// compile time.
    /// </summary>
    /// <typeparam name="TMessage">The message's compile-time type.</typeparam>
    /// <param name="message">The message to dispatch. Cannot be <c>null</c>.</param>
    /// <param name="serviceProvider">The provider participants are resolved against.</param>
    /// <param name="cancellationToken">Token for the dispatch.</param>
    /// <returns>A task that completes when the pipeline has run.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <c>null</c>.</exception>
    /// <remarks>
    /// <para>
    /// When <typeparamref name="TMessage"/> is the message's runtime type — the usual case
    /// — the executor comes from a static generic slot rather than a type-keyed lookup.
    /// Dispatching through a base type falls back to the runtime type, so behavior matches
    /// <see cref="DispatchAsync(object, IServiceProvider, CancellationToken, IEnumerable{string})"/>
    /// exactly. Group-filtered dispatches use that overload.
    /// </para>
    /// <para>
    /// The name differs from <c>DispatchAsync</c> deliberately. A same-named generic would
    /// join the candidates of every explicit <c>DispatchAsync&lt;T&gt;(msg, …)</c> call, and
    /// wherever the message converts to the type argument — which it does for a
    /// result-producing dispatch, since <c>ICommand&lt;TResult&gt;</c> derives from
    /// <c>ICommand</c> — it would outrank the result overload and quietly send the dispatch
    /// down the void path.
    /// </para>
    /// </remarks>
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
    /// Dispatches <paramref name="message"/> and returns the result its pipeline produced.
    /// </summary>
    /// <typeparam name="TResult">The result type the pipeline produces.</typeparam>
    /// <param name="message">The message to dispatch. Cannot be <c>null</c>.</param>
    /// <param name="serviceProvider">The provider participants are resolved against.</param>
    /// <param name="cancellationToken">Token for the dispatch.</param>
    /// <param name="groups">The groups to run; <c>null</c> uses the default group.</param>
    /// <returns>The result the pipeline produced.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <c>null</c>.</exception>
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
    /// Dispatches <paramref name="message"/> through its void pipeline under an execution
    /// context the caller owns, naming the message type at compile time.
    /// </summary>
    /// <typeparam name="TMessage">The message's compile-time type.</typeparam>
    /// <param name="message">The message to dispatch. Cannot be <c>null</c>.</param>
    /// <param name="context">The caller's execution context.</param>
    /// <param name="serviceProvider">The provider participants are resolved against.</param>
    /// <param name="groups">The groups to run; <c>null</c> uses the default group.</param>
    /// <returns>A task that completes when the pipeline has run.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <c>null</c>.</exception>
    public ValueTask DispatchVoidAsync<TMessage>(TMessage message, ErgosfareContext context,
        IServiceProvider serviceProvider, IEnumerable<string>? groups = null)
        where TMessage : IMessage
    {
        ArgumentNullException.ThrowIfNull(message);

        // Groups no longer pick the executor, so the compile-time slot serves a filtered
        // dispatch too; only the runtime-type check stands between the call and the field.
        var executor = message.GetType() == typeof(TMessage)
            ? _executorCache.GetVoidExecutor<TMessage>()
            : _executorCache.GetVoidExecutor(message.GetType());

        return executor.Execute(message, context, serviceProvider, groups);
    }

    /// <summary>
    /// Dispatches <paramref name="message"/> through its void pipeline under an execution
    /// context the caller owns.
    /// </summary>
    /// <param name="message">The message to dispatch. Cannot be <c>null</c>.</param>
    /// <param name="context">The caller's execution context. Cannot be <c>null</c>.</param>
    /// <param name="serviceProvider">The provider participants are resolved against.</param>
    /// <param name="groups">The groups to run; <c>null</c> uses the default group.</param>
    /// <returns>A task that completes when the pipeline has run.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="message"/> or <paramref name="context"/> is <c>null</c>.
    /// </exception>
    public ValueTask DispatchAsync(object message, ErgosfareContext context, IServiceProvider serviceProvider,
        IEnumerable<string>? groups = null)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(context);

        var executor = _executorCache.GetVoidExecutor(message.GetType());

        return executor.Execute(message, context, serviceProvider, groups);
    }

    /// <summary>
    /// Dispatches <paramref name="message"/> under a caller-owned execution context and
    /// returns the result its pipeline produced.
    /// </summary>
    /// <typeparam name="TResult">The result type the pipeline produces.</typeparam>
    /// <param name="message">The message to dispatch. Cannot be <c>null</c>.</param>
    /// <param name="context">The caller's execution context. Cannot be <c>null</c>.</param>
    /// <param name="serviceProvider">The provider participants are resolved against.</param>
    /// <param name="groups">The groups to run; <c>null</c> uses the default group.</param>
    /// <returns>The result the pipeline produced.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="message"/> or <paramref name="context"/> is <c>null</c>.
    /// </exception>
    public ValueTask<TResult> DispatchAsync<TResult>(object message, ErgosfareContext context, IServiceProvider serviceProvider,
        IEnumerable<string>? groups = null)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(context);

        var executor = _executorCache.GetExecutor<TResult>(message.GetType());

        return executor.Execute(message, context, serviceProvider, groups);
    }

    /// <summary>
    /// Dispatches <paramref name="message"/> and returns its result, naming both the
    /// message and result types at compile time.
    /// </summary>
    /// <typeparam name="TMessage">The message's compile-time type.</typeparam>
    /// <typeparam name="TResult">The result type the pipeline produces.</typeparam>
    /// <param name="message">The message to dispatch. Cannot be <c>null</c>.</param>
    /// <param name="serviceProvider">The provider participants are resolved against.</param>
    /// <param name="cancellationToken">Token for the dispatch.</param>
    /// <param name="groups">The groups to run; <c>null</c> uses the default group.</param>
    /// <returns>The result the pipeline produced.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <c>null</c>.</exception>
    /// <remarks>
    /// <para>
    /// Naming the message type removes the whole lookup — the <c>GetType()</c>, the
    /// (message, result) hash, and the table walk behind them — leaving a static field read
    /// that both the JIT and Native AOT resolve directly.
    /// </para>
    /// <para>
    /// Unlike <see cref="DispatchVoidAsync{TMessage}(TMessage, IServiceProvider, CancellationToken)"/>
    /// this can safely be an overload: <typeparamref name="TResult"/> cannot be inferred, so
    /// the member never joins a candidate set unless the caller named both arguments. The
    /// runtime-type check stays for the same reason the void path keeps one — naming a base
    /// type is legal, and the pipeline that runs belongs to the runtime type.
    /// </para>
    /// </remarks>
    public ValueTask<TResult> DispatchAsync<TMessage, TResult>(TMessage message, IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default,
        IEnumerable<string>? groups = null)
        where TMessage : IMessage
    {
        ArgumentNullException.ThrowIfNull(message);

        var executor = message.GetType() == typeof(TMessage)
            ? _executorCache.GetExecutor<TMessage, TResult>()
            : _executorCache.GetExecutor<TResult>(message.GetType());
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
    /// Dispatches <paramref name="message"/> under a caller-owned execution context and
    /// returns its result, naming both types at compile time.
    /// </summary>
    /// <typeparam name="TMessage">The message's compile-time type.</typeparam>
    /// <typeparam name="TResult">The result type the pipeline produces.</typeparam>
    /// <param name="message">The message to dispatch. Cannot be <c>null</c>.</param>
    /// <param name="context">The caller's execution context. Cannot be <c>null</c>.</param>
    /// <param name="serviceProvider">The provider participants are resolved against.</param>
    /// <param name="groups">The groups to run; <c>null</c> uses the default group.</param>
    /// <returns>The result the pipeline produced.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="message"/> or <paramref name="context"/> is <c>null</c>.
    /// </exception>
    public ValueTask<TResult> DispatchAsync<TMessage, TResult>(TMessage message, ErgosfareContext context,
        IServiceProvider serviceProvider, IEnumerable<string>? groups = null)
        where TMessage : IMessage
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(context);

        var executor = message.GetType() == typeof(TMessage)
            ? _executorCache.GetExecutor<TMessage, TResult>()
            : _executorCache.GetExecutor<TResult>(message.GetType());

        return executor.Execute(message, context, serviceProvider, groups);
    }
}
