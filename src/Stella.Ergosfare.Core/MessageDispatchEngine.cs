using System.Runtime.CompilerServices;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Factories;
using Stella.Ergosfare.Core.Internal.Contexts;
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
    /// The dependencies factory the executors build their pipeline plans against; exposed
    /// internally so the broadcast fast lane (events assembly, via InternalsVisibleTo) can
    /// key its cached plan by the same factory reference the executors use.
    /// </summary>
    private readonly IMessageDependenciesFactory _dependenciesFactory;

    internal MessageDispatchEngine(PipelineExecutorCache executorCache, IMessageDependenciesFactory dependenciesFactory)
    {
        _executorCache = executorCache ?? throw new ArgumentNullException(nameof(executorCache));
        _dependenciesFactory = dependenciesFactory ?? throw new ArgumentNullException(nameof(dependenciesFactory));
    }

    /// <inheritdoc cref="_dependenciesFactory"/>
    internal IMessageDependenciesFactory DependenciesFactory => _dependenciesFactory;

    /// <summary>
    /// Dispatches a void message through its cached pipeline executor, resolving handlers
    /// against <paramref name="serviceProvider"/> — the caller's scope.
    /// </summary>
    /// <param name="message">The message to dispatch.</param>
    /// <param name="serviceProvider">The scope provider handlers resolve against.</param>
    /// <param name="items">Optional contextual items exposed to the pipeline.</param>
    /// <param name="cancellationToken">Cancellation token for the dispatch.</param>
    /// <param name="groups">Optional group filters applied to the pipeline.</param>
    public ValueTask DispatchAsync(object message, IServiceProvider serviceProvider,
        IDictionary<object, object?>? items = null, CancellationToken cancellationToken = default,
        IEnumerable<string>? groups = null)
    {
        ArgumentNullException.ThrowIfNull(message);

        var executor = _executorCache.GetVoidExecutor(message.GetType(), groups);
        var context = ErgosfareExecutionContextPool.Rent(items, cancellationToken);
        ValueTask task;

        try
        {
            task = executor.Execute(message, context, serviceProvider);
        }
        catch
        {
            ErgosfareExecutionContextPool.Return(context);
            throw;
        }

        // Synchronously completed dispatches (the common case) return the context inline —
        // no async state machine on the hot path. Only a genuinely suspended pipeline pays
        // for the awaiting helper.
        if (task.IsCompletedSuccessfully)
        {
            ErgosfareExecutionContextPool.Return(context);
            return default;
        }

        return AwaitAndReturn(task, context);

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
        static async ValueTask AwaitAndReturn(ValueTask task, ErgosfareExecutionContext context)
        {
            try
            {
                await task;
            }
            finally
            {
                ErgosfareExecutionContextPool.Return(context);
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
    /// <param name="items">Optional contextual items exposed to the pipeline.</param>
    /// <param name="cancellationToken">Cancellation token for the dispatch.</param>
    public ValueTask DispatchVoidAsync<TMessage>(TMessage message, IServiceProvider serviceProvider,
        IDictionary<object, object?>? items = null, CancellationToken cancellationToken = default)
        where TMessage : IMessage
    {
        ArgumentNullException.ThrowIfNull(message);

        var executor = message.GetType() == typeof(TMessage)
            ? _executorCache.GetVoidExecutor<TMessage>()
            : _executorCache.GetVoidExecutor(message.GetType());
        var context = ErgosfareExecutionContextPool.Rent(items, cancellationToken);
        ValueTask task;

        try
        {
            task = executor.Execute(message, context, serviceProvider);
        }
        catch
        {
            ErgosfareExecutionContextPool.Return(context);
            throw;
        }

        if (task.IsCompletedSuccessfully)
        {
            ErgosfareExecutionContextPool.Return(context);
            return default;
        }

        return AwaitAndReturn(task, context);

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
        static async ValueTask AwaitAndReturn(ValueTask task, ErgosfareExecutionContext context)
        {
            try
            {
                await task;
            }
            finally
            {
                ErgosfareExecutionContextPool.Return(context);
            }
        }
    }

    /// <summary>
    /// Result-producing counterpart of
    /// <see cref="DispatchAsync(object, IServiceProvider, IDictionary{object, object?}?, CancellationToken, IEnumerable{string}?)"/>.
    /// </summary>
    /// <typeparam name="TResult">The expected result type of the message.</typeparam>
    public ValueTask<TResult> DispatchAsync<TResult>(object message, IServiceProvider serviceProvider,
        IDictionary<object, object?>? items = null, CancellationToken cancellationToken = default,
        IEnumerable<string>? groups = null)
    {
        ArgumentNullException.ThrowIfNull(message);

        var executor = _executorCache.GetExecutor<TResult>(message.GetType(), groups);
        var context = ErgosfareExecutionContextPool.Rent(items, cancellationToken);
        ValueTask<TResult> task;

        try
        {
            task = executor.Execute(message, context, serviceProvider);
        }
        catch
        {
            ErgosfareExecutionContextPool.Return(context);
            throw;
        }

        if (task.IsCompletedSuccessfully)
        {
            var result = task.Result;
            ErgosfareExecutionContextPool.Return(context);
            return new ValueTask<TResult>(result);
        }

        return AwaitAndReturn(task, context);

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
        static async ValueTask<TResult> AwaitAndReturn(ValueTask<TResult> task, ErgosfareExecutionContext context)
        {
            try
            {
                return await task;
            }
            finally
            {
                ErgosfareExecutionContextPool.Return(context);
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
    public ValueTask DispatchAsync(object message, IExecutionContext context, IServiceProvider serviceProvider,
        IEnumerable<string>? groups = null)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(context);

        var executor = _executorCache.GetVoidExecutor(message.GetType(), groups);

        return executor.Execute(message, context, serviceProvider);
    }

    /// <summary>
    /// Result-producing counterpart of
    /// <see cref="DispatchAsync(object, IExecutionContext, IServiceProvider, IEnumerable{string}?)"/>.
    /// </summary>
    /// <typeparam name="TResult">The expected result type of the message.</typeparam>
    public ValueTask<TResult> DispatchAsync<TResult>(object message, IExecutionContext context, IServiceProvider serviceProvider,
        IEnumerable<string>? groups = null)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(context);

        var executor = _executorCache.GetExecutor<TResult>(message.GetType(), groups);

        return executor.Execute(message, context, serviceProvider);
    }
}
