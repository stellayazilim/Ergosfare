using System;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
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
    /// Mediates the message by invoking the handler along with pre-, post-, exception-, and final interceptors.
    /// Supports checkpointing to skip already executed handlers, integrates snapshot results, 
    /// and ensures proper execution sequencing for fire-and-forget or retryable pipelines.
    /// </summary>
    /// <param name="message">The message to be handled. This may be transformed by pre-interceptors.</param>
    /// <param name="messageDependencies">The dependencies of the message, including registered handlers and interceptors.</param>
    /// <param name="context">The current execution context containing checkpoints, snapshots, and pipeline state.</param>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> representing the asynchronous operation, returning the final result 
    /// after executing the handler and all applicable interceptors.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="messageDependencies"/> is null.</exception>
    /// <exception cref="MultipleHandlerFoundException">Thrown if more than one handler is registered for the message.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the handler is null or not of the expected type, or if a result is missing after 
    /// execution when the pipeline is aborted.
    /// </exception>
    /// <remarks>
    /// <para>The mediation process follows this sequence:</para>
    /// <list type="number">
    /// <item>Invoke pre-interceptors via <see cref="PreInterceptorInvocationStrategy{TMessage}"/>. Each pre-interceptor
    /// can transform the message, and results are checkpointed to allow skipping on retries.</item>
    /// <item>Invoke the main handler. The result is checkpointed, and snapshots may be used to skip execution if already available.</item>
    /// <item>Invoke post-interceptors via <see cref="PostInterceptorInvocationStrategy{TMessage, TResult}"/>. Post-interceptors always execute
    /// and are not checkpointed.</item>
    /// <item>If an exception occurs, invoke <see cref="ExceptionInterceptorInvocationStrategy{TMessage, TResult}"/> to allow exception handling
    /// or transformation of the result.</item>
    /// <item>Finally, invoke <see cref="FinalInterceptorInvocationStrategy{TMessage, TResult}"/> for cleanup or final actions,
    /// which always execute regardless of prior success or failure.</item>
    /// <item>Checkpoints and snapshots ensure that retries, partial execution, or snapshot-based replay work seamlessly.</item>
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