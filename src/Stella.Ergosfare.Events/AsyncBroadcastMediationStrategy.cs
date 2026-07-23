using System.Runtime.ExceptionServices;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;
using Stella.Ergosfare.Core.Abstractions.Strategies;
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
///   <item><description>All handlers for the message — including those registered for its
///   base types and interfaces — are invoked sequentially, direct registrations first.</description></item>
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
    EventMediationSettings settings)
    : IMessageMediationStrategy<TMessage, ValueTask>
    where TMessage : notnull
{
    /// <summary>
    ///     Mediates the given message by broadcasting it sequentially to all registered
    ///     handlers: the event's own handlers first, then the covariantly matched ones
    ///     (registered against a base type or interface of the event).
    /// </summary>
    /// <param name="message">The message to be processed.</param>
    /// <param name="messageDependencies">
    ///     The dependencies required for message handling, including the registered handlers
    ///     and the pre-, post-, exception- and final-interceptor stages.
    /// </param>
    /// <param name="context">The current execution context.</param>
    /// <param name="serviceProvider">The provider of the scope this dispatch runs in; handlers and interceptors resolve from it.</param>
    /// <returns>A ValueTask representing the asynchronous operation of the mediation process.</returns>
    /// <remarks>
    ///     Opting a handler out of broad delivery is a group concern: an indirect handler
    ///     carrying a non-default <c>[Group]</c> only runs when a publish selects its group —
    ///     the group filter is applied while the pipeline shape is built.
    /// </remarks>
    public async ValueTask Mediate(TMessage message, IMessageDependencies messageDependencies, IExecutionContext context, IServiceProvider serviceProvider)
    {

        var handlers = FilterHandlers(messageDependencies.Handlers);
        var indirectHandlers = FilterHandlers(messageDependencies.IndirectHandlers);

        if (handlers.Count == 0 && indirectHandlers.Count == 0)
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
            await PublishSequentially(message, indirectHandlers, context, serviceProvider);

            // A ValueTask may be awaited only once — the completed ValueTask stands in as the
            // (meaningless for events) result object flowing through the interceptor stages.
            var postInvoker = new PostInterceptorInvocationStrategy<TMessage, ValueTask>(messageDependencies, null, serviceProvider);
            await postInvoker.Invoke(message, CompletedResultBox.Instance, context);
        }
        catch (Exception e)
        {
            exception = e;
            var exceptionInvoker = new ExceptionInterceptorInvocationStrategy<TMessage, ValueTask>(messageDependencies, serviceProvider);
            await exceptionInvoker.Invoke(message, CompletedResultBox.Instance,
                ExceptionDispatchInfo.Capture(e), context);

        }

        finally
        {
            var finalInvoker = new FinalInterceptorInvocationStrategy<TMessage, ValueTask>(messageDependencies, serviceProvider);
            await finalInvoker.Invoke(message, CompletedResultBox.Instance, exception, context);
        }
    }

    /// <summary>
    /// Applies the settings' handler filter without LINQ: the common case — a predicate
    /// that accepts every handler — returns the original list with zero allocation; a
    /// filtering predicate materializes a list only from the first rejection onward.
    /// </summary>
    private IReadOnlyList<IHandlerReference<IHandler, IMainHandlerDescriptor>> FilterHandlers(
        IReadOnlyList<IHandlerReference<IHandler, IMainHandlerDescriptor>> handlers)
    {
        var predicate = settings.Filters.HandlerPredicate;
        List<IHandlerReference<IHandler, IMainHandlerDescriptor>>? filtered = null;

        for (var i = 0; i < handlers.Count; i++)
        {
            var handler = handlers[i];

            if (predicate(handler.Descriptor.HandlerType))
            {
                filtered?.Add(handler);
            }
            else if (filtered is null)
            {
                // First rejection: materialize the accepted prefix and filter from here on.
                filtered = new List<IHandlerReference<IHandler, IMainHandlerDescriptor>>(handlers.Count - 1);
                for (var j = 0; j < i; j++)
                {
                    filtered.Add(handlers[j]);
                }
            }
        }

        return filtered ?? handlers;
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