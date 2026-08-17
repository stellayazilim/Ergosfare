using System.Runtime.ExceptionServices;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Abstractions.Strategies.InvocationStrategies;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// Runs a result-producing message through the single handler that serves it and the
/// interceptor stages around it.
/// </summary>
/// <typeparam name="TMessage">The message type being dispatched.</typeparam>
/// <typeparam name="TResult">The result type the pipeline produces.</typeparam>
/// <remarks>
/// The counterpart of <see cref="VoidPipelineBody{TMessage}"/>, with a result that means
/// something: the result can carry a failure, the post and exception stages can replace it,
/// and a result type that can be built from an exception absorbs failures rather than
/// letting them reach the caller.
/// </remarks>
#pragma warning disable CS8714 // TResult is used as a pattern type argument; handler contracts declare notnull results
internal static class ResultPipelineBody<TMessage, TResult> where TMessage : notnull
{
    /// <summary>
    /// Runs the pipeline for <paramref name="message"/> and returns its result.
    /// </summary>
    /// <param name="message">The message to dispatch.</param>
    /// <param name="messageDependencies">The message's participants, per stage.</param>
    /// <param name="resultAdapter">
    /// The result type's adapter, used to spot a failure carried in the result; <c>null</c>
    /// when there is none.
    /// </param>
    /// <param name="resultMaterializer">
    /// The result type's materializer. When present, a thrown failure comes back inside the
    /// result instead of reaching the caller; <c>null</c> keeps the default of throwing.
    /// </param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <param name="serviceProvider">The provider participants are resolved from.</param>
    /// <returns>The result the pipeline produced.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="messageDependencies"/> is <c>null</c>.</exception>
    /// <exception cref="MultipleHandlerFoundException">
    /// The level of handlers that serves the message holds more than one.
    /// </exception>
    /// <exception cref="NoHandlerFoundException">No handler is registered for the message.</exception>
    /// <exception cref="ExecutionAbortedException">A participant stopped the pipeline.</exception>
    /// <exception cref="InvalidOperationException">
    /// An exception interceptor handled the failure but produced no result, leaving the
    /// dispatch with nothing to return.
    /// </exception>
    internal static async ValueTask<TResult> Run(
        TMessage message,
        IMessageDependencies messageDependencies,
        IResultAdapter<TResult>? resultAdapter,
        IResultMaterializer<TResult>? resultMaterializer,
        ErgosfareContext context,
        IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(messageDependencies);

        // Handlers registered for the message type itself win outright, and only when there
        // are none does the covariant level come into play; see the void body.
        var handlers = messageDependencies.Handlers;
        var indirectHandlers = messageDependencies.IndirectHandlers;

        if (handlers.Count > 1)
        {
            throw new MultipleHandlerFoundException(typeof(TMessage), handlers.Count);
        }

        if (handlers.Count == 0 && indirectHandlers.Count > 1)
        {
            throw new MultipleHandlerFoundException(typeof(TMessage), indirectHandlers.Count);
        }

        if (handlers.Count == 0 && indirectHandlers.Count == 0)
        {
            throw new NoHandlerFoundException(typeof(TMessage), $"No handler is registered for {typeof(TMessage).Name}.");
        }

        var soleHandler = handlers.Count == 1 ? handlers[0] : indirectHandlers[0];

        var preInterceptorCount = messageDependencies.PreInterceptors.Count;
        var postInterceptorCount = messageDependencies.PostInterceptors.Count;
        var exceptionInterceptorCount = messageDependencies.ExceptionInterceptors.Count;
        var finalInterceptorCount = messageDependencies.FinalInterceptors.Count;

        // With no interceptors at all there is nobody to observe or change anything, so the
        // handler is called on its own and a failure propagates untouched.
        if ((preInterceptorCount | postInterceptorCount | exceptionInterceptorCount | finalInterceptorCount) == 0)
        {
            var fastHandler = soleHandler.Resolve(serviceProvider);

            TResult fastResult;

            try
            {
                // No abort arm: a handler that stops its own dispatch has no stage left to
                // skip and nothing left to report, so the signal goes straight to the caller.
                fastResult = await InvokeHandler(fastHandler, message, context);
            }
            catch (Exception e) when (resultMaterializer is not null && e is not ExecutionAbortedException)
            {
                // A result type that can be built from an exception never lets one reach the
                // caller: the failure comes back inside the result.
                return resultMaterializer.Materialize(e);
            }

            // A failure carried in the result, with no interceptor to hand it to. A result
            // type that can hold it flows out as it is for the caller to inspect; any other
            // keeps the default and throws.
            if (resultMaterializer is null
                && resultAdapter is not null
                && resultAdapter.TryGetException(in fastResult, out var fastEx) && fastEx is not null)
            {
                throw fastEx;
            }

            return fastResult;
        }

        TResult result = default!;
        Exception? exception = null;
        ExceptionDispatchInfo? unhandledException = null;
        var aborted = false;
        try
        {
            try
            {
                if (preInterceptorCount > 0)
                {
                    message = (TMessage) await PreInterceptorInvocationStrategy<TMessage>.Invoke(
                        messageDependencies, serviceProvider, message, context);
                }

                var handler = soleHandler.Resolve(serviceProvider);

                result = await InvokeHandler(handler, message, context);

                // A failure carried in the result reaches the exception stage below without
                // anything being thrown.
                if (resultAdapter is not null && resultAdapter.TryGetException(in result, out var carried) && carried is not null)
                {
                    exception = carried;
                }

                if (exception is null && postInterceptorCount > 0)
                {
                    var (postResult, postCarried) = await PostInterceptorInvocationStrategy<TMessage, TResult>.Invoke(
                        messageDependencies, resultAdapter, serviceProvider, message, result, context);

                    if (postCarried is not null)
                    {
                        // The failed result a post-interceptor produced is the pipeline's
                        // result from here on; the stage already skipped the ones after it.
                        result = (TResult)postResult!;
                        exception = postCarried;
                    }
                    else
                    {
                        var typedPostResult = (TResult?)postResult;
                        result = typedPostResult is null ? result : typedPostResult;
                    }
                }
            }
            catch (Exception e) when (e is not ExecutionAbortedException)
            {
                // With no exception interceptors and no adapter, the failure keeps its
                // original path out of the pipeline.
                if (exceptionInterceptorCount == 0 && resultAdapter is null)
                {
                    exception = e;
                    throw;
                }

                exception = e;

                if (resultMaterializer is not null)
                {
                    // The failure becomes a failed result before the exception stage sees
                    // it, so a stage that declines leaves that result standing and the
                    // caller still never sees a throw.
                    result = resultMaterializer.Materialize(e);
                }
                else
                {
                    unhandledException = ExceptionDispatchInfo.Capture(e);
                }
            }

            if (exception is not null)
            {
                var matched = false;

                if (exceptionInterceptorCount > 0)
                {
                    (matched, var stageResult) = await ExceptionInterceptorInvocationStrategy<TMessage, TResult>.Invoke(
                        messageDependencies, serviceProvider, message, result, exception, context);

                    if (matched)
                    {
                        // An interceptor that handled the failure owns the result: there is
                        // nothing to fall back on, because the handler threw and the result
                        // is still its default. Answering null here would hand the caller
                        // nothing for a type that promised a value, with the failure gone
                        // too — so the pipeline says what happened instead.
                        if (stageResult is null)
                        {
                            throw new InvalidOperationException(
                                $"An exception interceptor handled the failure of '{typeof(TMessage)}' and returned no result. " +
                                $"The dispatch is typed '{typeof(TResult)}' and cannot answer with null: produce a result, " +
                                "or leave the failure unhandled so it surfaces to the caller.");
                        }

                        result = (TResult)stageResult;
                    }
                }

                if (matched)
                {
                    unhandledException = null;
                }
                else if (resultMaterializer is null)
                {
                    // Nobody accepted the failure and the result type cannot hold one, so it
                    // is thrown after the final stage. A carried failure was never thrown, so
                    // this is where its dispatch stack starts.
                    unhandledException ??= ExceptionDispatchInfo.Capture(exception);
                }
            }
        }
        catch (ExecutionAbortedException)
        {
            // A participant stopped the pipeline. Nothing else runs — neither the exception
            // stage nor the final stage below — and the signal continues to the caller,
            // which is why no result leaves here either.
            aborted = true;
            throw;
        }
        finally
        {
            if (finalInterceptorCount > 0 && !aborted)
            {
                await FinalInterceptorInvocationStrategy<TMessage, TResult>.Invoke(
                    messageDependencies, serviceProvider, message, result, exception, context);
            }
        }

        unhandledException?.Throw();

        return result;
    }

    /// <summary>
    /// Calls the handler through whichever main-handler contract it implements.
    /// </summary>
    /// <param name="handler">The resolved handler.</param>
    /// <param name="message">The message to hand it.</param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <returns>The result the handler produced.</returns>
    /// <exception cref="NotSupportedException">
    /// The handler implements no main-handler contract accepting
    /// <typeparamref name="TMessage"/> and producing <typeparamref name="TResult"/>, which
    /// happens when a message is dispatched through a static type that erases its own.
    /// </exception>
    private static ValueTask<TResult> InvokeHandler(object handler, TMessage message, ErgosfareContext context)
        => handler switch
        {
            IAsyncHandler<TMessage, TResult> asyncHandler => asyncHandler.HandleAsync(message, context),
            IHandler<TMessage, ValueTask<TResult>> valueTaskShaped => valueTaskShaped.Handle(message, context),
            IHandler<TMessage, TResult> syncHandler => ValueTask.FromResult(syncHandler.Handle(message, context)),
            _ => throw new NotSupportedException(
                $"'{handler.GetType()}' does not implement a supported handler contract for message '{typeof(TMessage)}' and result '{typeof(TResult)}'. " +
                "Interface-erased dispatch is not supported; dispatch with the concrete message type."),
        };
}
#pragma warning restore CS8714
