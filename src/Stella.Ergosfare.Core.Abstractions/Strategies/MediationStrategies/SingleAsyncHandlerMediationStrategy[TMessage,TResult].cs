using System.Runtime.ExceptionServices;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Abstractions.Strategies.InvocationStrategies;

namespace Stella.Ergosfare.Core.Abstractions.Strategies;

/// <summary>
/// Implements a mediation strategy for a single asynchronous handler.
/// Ensures that only one handler is executed for the message, invokes pre- and post-interceptors,
/// handles exceptions, and applies final interceptors. Supports optional result adaptation.
/// </summary>
/// <typeparam name="TMessage">The type of the message being handled.</typeparam>
/// <typeparam name="TResult">The type of the result returned by the handler.</typeparam>
public sealed class SingleAsyncHandlerMediationStrategy<TMessage, TResult>(IResultAdapterService? resultAdapterService) : IMessageMediationStrategy<TMessage, ValueTask<TResult>> 
    where TMessage : notnull
{
    
    /// <summary>
    /// Mediates the message by invoking the single registered handler along with the pre-,
    /// post-, exception- and final-interceptor stages, applying optional result adaptation.
    /// </summary>
    /// <param name="message">The message to be handled. May be transformed by pre-interceptors.</param>
    /// <param name="messageDependencies">The dependencies of the message, including the registered handler and interceptor stages.</param>
    /// <param name="context">The current execution context.</param>
    /// <param name="serviceProvider">The provider of the scope this dispatch runs in; handlers and interceptors resolve from it.</param>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> representing the asynchronous operation, returning the final result
    /// after executing the handler and all applicable interceptors.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="messageDependencies"/> is null.</exception>
    /// <exception cref="MultipleHandlerFoundException">Thrown if more than one handler is registered for the message.</exception>
    /// <exception cref="InvalidOperationException">Thrown if no handler is registered for the message.</exception>
    /// <remarks>
    /// <para>The mediation process follows this sequence:</para>
    /// <list type="number">
    /// <item>With no interceptors registered, the handler is invoked directly on a fast path
    /// and exceptions propagate unchanged.</item>
    /// <item>Pre-interceptors run via <see cref="PreInterceptorInvocationStrategy{TMessage}"/>;
    /// each may transform the message.</item>
    /// <item>The main handler runs through its typed contract; the result adapter may surface
    /// a failure carried inside the result as an exception.</item>
    /// <item>Post-interceptors run via <see cref="PostInterceptorInvocationStrategy{TMessage, TResult}"/>
    /// and may replace the result.</item>
    /// <item>On exception, <see cref="ExceptionInterceptorInvocationStrategy{TMessage, TResult}"/> runs —
    /// with no exception interceptors registered the exception propagates unchanged; an
    /// <see cref="ExecutionAbortedException"/> aborts without error.</item>
    /// <item><see cref="FinalInterceptorInvocationStrategy{TMessage, TResult}"/> always runs last,
    /// regardless of success or failure.</item>
    /// </list>
    /// </remarks>
    public async ValueTask<TResult> Mediate(TMessage message, IMessageDependencies messageDependencies, IExecutionContext context, IServiceProvider serviceProvider)
    {
        if (messageDependencies is null)
        {
            throw new ArgumentNullException(nameof(messageDependencies));
        }
        if (messageDependencies.Handlers.Count > 1)
        {
            throw new MultipleHandlerFoundException(typeof(TMessage), messageDependencies.Handlers.Count);
        }

        if (messageDependencies.Handlers.Count == 0)
        {
            throw new InvalidOperationException($"No handler is registered for {typeof(TMessage).Name}.");
        }

        var preInterceptorCount = messageDependencies.PreInterceptors.Count;
        var postInterceptorCount = messageDependencies.PostInterceptors.Count;
        var exceptionInterceptorCount = messageDependencies.ExceptionInterceptors.Count;
        var finalInterceptorCount = messageDependencies.FinalInterceptors.Count;

        // Fast path: with no interceptors registered, none of the invocation strategies can
        // observe or transform anything — invoke the handler directly. Exceptions propagate
        // unchanged, matching the zero-interceptor rethrow behavior of the full pipeline.
        if ((preInterceptorCount | postInterceptorCount | exceptionInterceptorCount | finalInterceptorCount) == 0)
        {
            // Typed seam: when the dispatch TMessage is the handler's message type (or a
            // derived one — IHandler's `in TMessage` variance covers that), invoke the typed
            // member directly and skip the object-typed DIM bridge. Interface-erased
            // dispatches (TMessage = ICommand<T> etc.) fall back to the bridge.
            var fastHandler = messageDependencies.Handlers[0].Resolve(serviceProvider);

            var fastResult = await InvokeHandler(fastHandler, message, context);

            var fastEx = resultAdapterService?.LookupException(fastResult);
            if (fastEx is not null) throw fastEx;

            return fastResult;
        }

        TResult result = default!;
        Exception? exception = null;
        try
        {
            if (preInterceptorCount > 0)
            {
                var preInvoker = new PreInterceptorInvocationStrategy<TMessage>(messageDependencies, serviceProvider);
                message = (TMessage) await preInvoker.Invoke(message, context);
            }

            var handler = messageDependencies.Handlers[0].Resolve(serviceProvider);

            result = await InvokeHandler(handler, message, context);

            var ex = resultAdapterService?.LookupException(result);
            if (ex is not null) throw ex;


            if (postInterceptorCount > 0)
            {
                var postInvoker = new PostInterceptorInvocationStrategy<TMessage, TResult>(messageDependencies, resultAdapterService, serviceProvider);

                var postResult = (TResult?)await postInvoker.Invoke(message, result, context);
                result = postResult is null ? result : postResult;
            }
        }
        catch (ExecutionAbortedException)
        {
            throw;
        }
        catch (Exception e) when (e is not ExecutionAbortedException)
        {
            exception = e;

            if (exceptionInterceptorCount == 0)
            {
                throw;
            }

            var exceptionInvoker = new ExceptionInterceptorInvocationStrategy<TMessage, TResult>(messageDependencies, serviceProvider);
            var exceptionResult  = (TResult?)await exceptionInvoker.Invoke(
                message,
                result,
                ExceptionDispatchInfo.Capture(exception),
                context);

            result = exceptionResult is null ? result : exceptionResult;

        }
        finally
        {
            if (finalInterceptorCount > 0)
            {
                var finalInvoker = new FinalInterceptorInvocationStrategy<TMessage, TResult>(messageDependencies, serviceProvider);
                await finalInvoker.Invoke(message, result, exception, context);
            }
        }

        return result;
    }

    /// <summary>
    /// Invokes the handler through its typed contract. There is no object-typed bridge:
    /// asynchronous handlers are called via <c>IAsyncHandler</c>, synchronous handlers via
    /// <see cref="IHandler{TMessage, TResult}"/> (`in TMessage` variance admits handlers
    /// registered for base message types). Interface-erased dispatch is unsupported —
    /// dispatch with the concrete message type (the executor path) instead.
    /// </summary>
#pragma warning disable CS8714 // TResult is used as a pattern type argument; handlers declare notnull results
    private static ValueTask<TResult> InvokeHandler(object handler, TMessage message, IExecutionContext context)
        => handler switch
        {
            IAsyncHandler<TMessage, TResult> asyncHandler => asyncHandler.HandleAsync(message, context),
            IHandler<TMessage, ValueTask<TResult>> valueTaskShaped => valueTaskShaped.Handle(message, context),
            IHandler<TMessage, TResult> syncHandler => ValueTask.FromResult(syncHandler.Handle(message, context)),
            _ => throw new NotSupportedException(
                $"'{handler.GetType()}' does not implement a supported handler contract for message '{typeof(TMessage)}' and result '{typeof(TResult)}'. " +
                "Interface-erased dispatch is not supported; dispatch with the concrete message type."),
        };
#pragma warning restore CS8714
}