using System.Runtime.CompilerServices;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Internal.Extensions;

namespace Stella.Ergosfare.Core;

/// <summary>
/// The dispatch machinery behind every mediator: it finds the pipeline for a message, runs
/// it under an execution context, and hands back the result.
/// </summary>
internal sealed class MessageDispatchEngine
{
    internal global::Stella.Ergosfare.Core.Abstractions.Planning.DispatchPlanCatalog Catalog { get; }

    internal MessageDispatchEngine(global::Stella.Ergosfare.Core.Abstractions.Planning.DispatchPlanCatalog catalog)
    {
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.PreparePlans();
    }

    /// <summary>
    /// Publishes <paramref name="message"/> to every handler of its pipeline.
    /// </summary>
    /// <param name="message">The event to publish. Cannot be <c>null</c>.</param>
    /// <param name="serviceProvider">The provider handlers are resolved against.</param>
    /// <param name="cancellationToken">Token for the delivery.</param>
    /// <param name="groups">The groups to deliver to; <c>null</c> uses the default group.</param>
    /// <returns>A task that completes when every handler has run.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <c>null</c>.</exception>
    public ValueTask BroadcastAsync(object message, IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default,
        GroupSet? groups = null)
    {
        ArgumentNullException.ThrowIfNull(message);

        return PublishPooled(message, serviceProvider, cancellationToken, groups);
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
    public ValueTask BroadcastAsync<TMessage>(TMessage message, IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default,
        GroupSet? groups = null)
        where TMessage : notnull
    {
        ArgumentNullException.ThrowIfNull(message);

        return PublishPooled(message, serviceProvider, cancellationToken, groups);
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
        GroupSet? groups = null)
    {
        ArgumentNullException.ThrowIfNull(message);

        return this.BroadcastPlan(message, context, serviceProvider, groups);
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
    public IAsyncEnumerable<TResult> StreamAsync<TResult>(object query, IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default,
        GroupSet? groups = null)
    {
        ArgumentNullException.ThrowIfNull(query);

        return this.StreamPlan<TResult>(query, new ErgosfareContext(cancellationToken: cancellationToken),
            serviceProvider, groups);
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
        IServiceProvider serviceProvider, GroupSet? groups = null)
    {
        ArgumentNullException.ThrowIfNull(query);

        return this.StreamPlan<TResult>(query, context, serviceProvider, groups);
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
        GroupSet? groups = null)
    {
        ArgumentNullException.ThrowIfNull(message);

        var context = ErgosfareContextPool.Rent(null, cancellationToken);
        ValueTask task;

        try
        {
            task = this.SendPlan(message, context, serviceProvider, groups);
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
    public ValueTask DispatchVoidAsync<TMessage>(TMessage message, IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
        where TMessage : IMessage
    {
        ArgumentNullException.ThrowIfNull(message);

        var context = ErgosfareContextPool.Rent(null, cancellationToken);
        ValueTask task;

        try
        {
            task = this.SendPlan(message, context, serviceProvider, null);
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
        GroupSet? groups = null)
    {
        ArgumentNullException.ThrowIfNull(message);

        var context = ErgosfareContextPool.Rent(null, cancellationToken);
        ValueTask<TResult> task;

        try
        {
            task = this.SendPlan<TResult>(message, context, serviceProvider, groups);
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
        IServiceProvider serviceProvider, GroupSet? groups = null)
        where TMessage : IMessage
    {
        ArgumentNullException.ThrowIfNull(message);

        return this.SendPlan(message, context, serviceProvider, groups);
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
        GroupSet? groups = null)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(context);


        return this.SendPlan(message, context, serviceProvider, groups);
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
        GroupSet? groups = null)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(context);


        return this.SendPlan<TResult>(message, context, serviceProvider, groups);
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
    public ValueTask<TResult> DispatchAsync<TMessage, TResult>(TMessage message, IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default,
        GroupSet? groups = null)
        where TMessage : IMessage
    {
        ArgumentNullException.ThrowIfNull(message);

        var context = ErgosfareContextPool.Rent(null, cancellationToken);
        ValueTask<TResult> task;

        try
        {
            task = this.SendPlan<TResult>(message, context, serviceProvider, groups);
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
        IServiceProvider serviceProvider, GroupSet? groups = null)
        where TMessage : IMessage
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(context);


        return this.SendPlan<TResult>(message, context, serviceProvider, groups);
    }
    private ValueTask PublishPooled(object message, IServiceProvider provider,
        CancellationToken cancellationToken, GroupSet? groups)
    {
        var context = ErgosfareContextPool.Rent(null, cancellationToken);
        ValueTask task;
        try { task = this.BroadcastPlan(message, context, provider, groups); }
        catch { ErgosfareContextPool.Return(context); throw; }
        if (task.IsCompletedSuccessfully)
        {
            task.GetAwaiter().GetResult();
            ErgosfareContextPool.Return(context);
            return default;
        }
        return Finish(task, context);

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
        static async ValueTask Finish(ValueTask task, ErgosfareContext context)
        {
            try { await task.ConfigureAwait(false); }
            finally { ErgosfareContextPool.Return(context); }
        }
    }

}
