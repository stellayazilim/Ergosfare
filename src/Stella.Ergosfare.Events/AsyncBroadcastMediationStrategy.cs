using System.Runtime.ExceptionServices;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;
using Stella.Ergosfare.Core.Abstractions.Strategies.InvocationStrategies;
using Stella.Ergosfare.Events.Abstractions;

namespace Stella.Ergosfare.Events;


/// <summary>
/// Represents a mediation strategy that broadcasts a message asynchronously
/// to all registered handlers of the specified <typeparamref name="TMessage"/>.
/// </summary>
/// <typeparam name="TMessage">The type of message being mediated.</typeparam>
/// <remarks>
/// <para>
/// This strategy ensures that:
/// <list type="bullet">
///   <item><description>All handlers for the message are invoked sequentially.</description></item>
///   <item><description>Pre-, post-, exception-, and final-interceptors are executed in the correct order.</description></item>
///   <item><description>Exceptions are captured and passed to the configured exception interceptors.</description></item>
/// </list>
/// </para>
/// <para>
/// Since this strategy is intended for event broadcasting, results are not adapted
/// (unlike request/response message patterns).
/// </para>
/// </remarks>
public sealed class AsyncBroadcastMediationStrategy<TMessage>(
    IResultAdapterService? resultAdapterService,
    EventMediationSettings settings)
    : IMessageMediationStrategy<TMessage, ValueTask>
    where TMessage : notnull
{
    /// <summary>
    /// A completed <see cref="ValueTask"/> boxed once and reused as the broadcast's
    /// (meaningless for events) result object flowing through the interceptor stages.
    /// </summary>
    private static readonly object CompletedResultBox = ValueTask.CompletedTask;

    /// <summary>
    ///     Mediates the given message by broadcasting it to all registered handlers concurrently.
    /// </summary>
    /// <param name="message">The message to be processed.</param>
    /// <param name="messageDependencies">
    ///     The dependencies required for message handling, including registered handlers,
    ///     pre-handlers, post-handlers, and error handlers.
    /// </param>
    /// <param name="context"></param>
    /// <returns>A ValueTask representing the asynchronous operation of the mediation process.</returns>
    public async ValueTask Mediate(TMessage message, IMessageDependencies messageDependencies, IExecutionContext context, IServiceProvider serviceProvider)
    {

        var handlers = messageDependencies.Handlers
            .Where(x => settings.Filters.HandlerPredicate(x.Descriptor.HandlerType))
            .ToList();

        if (handlers.Count == 0)
        {
            if (settings.ThrowIfNoHandlerFound)
            {
                throw new NoHandlerFoundException(typeof(TMessage));
            }
            return;
        }
        Exception? exception = null;
        try
        {
            // events doesn't need result adapter, since events intended to not return a result
            var preInvoker = new PreInterceptorInvocationStrategy<TMessage>(messageDependencies, serviceProvider);
            await preInvoker.Invoke(message, context);
            await PublishSequentially(message, handlers, context, serviceProvider);

            // A ValueTask may be awaited only once — the completed ValueTask stands in as the
            // (meaningless for events) result object flowing through the interceptor stages.
            var postInvoker = new PostInterceptorInvocationStrategy<TMessage, ValueTask>(messageDependencies, null, serviceProvider);
            await postInvoker.Invoke(message, CompletedResultBox, context);
        }
        catch (Exception e)
        {
            exception = e;
            var exceptionInvoker = new ExceptionInterceptorInvocationStrategy<TMessage, ValueTask>(messageDependencies, serviceProvider);
            await exceptionInvoker.Invoke(message, CompletedResultBox,
                ExceptionDispatchInfo.Capture(e), context);

        }

        finally
        {
            var finalInvoker = new FinalInterceptorInvocationStrategy<TMessage, ValueTask>(messageDependencies, serviceProvider);
            await finalInvoker.Invoke(message, CompletedResultBox, exception, context);
        }
    }

    
    /// <summary>
    /// Publishes the message sequentially to all resolved handlers.
    /// </summary>
    /// <param name="message">The message being handled.</param>
    /// <param name="handlers">The collection of handlers resolved for this message.</param>
    /// <param name="context">The execution context for this mediation pipeline.</param>
    /// <param name="serviceProvider">The provider of the scope this dispatch runs in.</param>
    private async ValueTask PublishSequentially(TMessage message, IReadOnlyList<IHandlerReference<IHandler, IMainHandlerDescriptor>> handlers, IExecutionContext context, IServiceProvider serviceProvider)
    {
        for (var i = 0; i < handlers.Count; i++)
        {
            // Typed dispatch only — no object bridge. `in TMessage` variance admits handlers
            // registered for base event types; erased dispatch goes through the executor.
            var handler = handlers[i].Resolve(serviceProvider);

            switch (handler)
            {
                case IAsyncHandler<TMessage> asyncHandler:
                    await asyncHandler.HandleAsync(message, context);
                    break;
                case IHandler<TMessage, ValueTask> valueTaskShaped:
                    await valueTaskShaped.Handle(message, context);
                    break;
                case IHandler<TMessage, object> syncHandler:
                    syncHandler.Handle(message, context);
                    break;
                default:
                    throw new NotSupportedException(
                        $"'{handler.GetType()}' does not implement a supported handler contract for event '{typeof(TMessage)}'. " +
                        "Interface-erased dispatch is not supported; publish with the concrete event type.");
            }
        }
    }
}