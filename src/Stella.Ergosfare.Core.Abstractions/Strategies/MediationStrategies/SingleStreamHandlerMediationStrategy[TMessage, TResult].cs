using System.Runtime.ExceptionServices;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Abstractions.Strategies.InvocationStrategies;

namespace Stella.Ergosfare.Core.Abstractions.Strategies;


/// <summary>
/// Mediates a message to the single stream handler that serves it, running the interceptor
/// stages around the stream: the pre-stage before the handler is invoked, the post-,
/// exception- and final stages after enumeration ends.
/// </summary>
/// <typeparam name="TMessage">The message type being mediated.</typeparam>
/// <typeparam name="TResult">The type of the items the handler streams.</typeparam>
/// <remarks>
/// An instance carries the state of one enumeration and serves a single dispatch.
/// </remarks>
/// <param name="cancellationToken">The token the stream is enumerated under.</param>
public sealed class SingleStreamHandlerMediationStrategy<TMessage, TResult>(
    CancellationToken cancellationToken) : IMessageMediationStrategy<TMessage, IAsyncEnumerable<TResult>>
    where TMessage : notnull
{

    /// <summary>
    /// The failure to hand to the exception stage: thrown by the handler, raised while
    /// enumerating, or carried inside a post-interceptor's result.
    /// </summary>
    Exception? _unknownException;

    /// <summary>
    /// Whether a participant stopped the pipeline. An aborted dispatch runs no final
    /// interceptors.
    /// </summary>
    bool _executionAborted;

    /// <summary>
    /// Whether enumeration should continue — cleared when the stream ends and when a
    /// failure makes further items pointless.
    /// </summary>
    bool _consume = true;


    /// <summary>
    /// Streams the results of handling <paramref name="message"/>, running the interceptor
    /// stages around the enumeration.
    /// </summary>
    /// <param name="message">The message to mediate.</param>
    /// <param name="messageDependencies">The message's participants, per stage.</param>
    /// <param name="context">The execution context of this dispatch.</param>
    /// <param name="serviceProvider">The provider participants are resolved from.</param>
    /// <returns>
    /// The streamed items. Nothing runs until the caller begins enumerating, and the
    /// stages after the handler run once enumeration ends.
    /// </returns>
    /// <exception cref="MultipleHandlerFoundException">
    /// Several handlers are registered at the level that serves the message.
    /// </exception>
    /// <exception cref="NoHandlerFoundException">No handler is registered for the message.</exception>
    /// <exception cref="ExecutionAbortedException">A participant stopped the pipeline.</exception>
    /// <exception cref="NotSupportedException">
    /// The selected handler implements no stream-handler contract for
    /// <typeparamref name="TMessage"/>.
    /// </exception>
    public async IAsyncEnumerable<TResult> Mediate(TMessage message, IMessageDependencies messageDependencies,
        ErgosfareContext context, IServiceProvider serviceProvider)
    {
        // Handlers registered for the message type itself decide the dispatch on their own;
        // only when there are none do handlers registered for a base type get considered.
        // Either level must hold exactly one handler.
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

        var handler = (handlers.Count == 1 ? handlers[0] : indirectHandlers[0]).Resolve(serviceProvider);

        IAsyncEnumerable<TResult>? enumerable = null;

        try
        {
            message = (TMessage) await PreInterceptorInvocationStrategy<TMessage>.Invoke(
                messageDependencies, serviceProvider, message, context);


            // Calling Handle only builds the sequence; the handler body runs as the caller
            // enumerates it below.
            enumerable = handler is IHandler<TMessage, IAsyncEnumerable<TResult>> typed
                ? typed.Handle(message, context)
                : throw new NotSupportedException(
                    $"'{handler.GetType()}' does not implement a supported stream handler contract for message '{typeof(TMessage)}'. " +
                    "Interface-erased dispatch is not supported; dispatch with the concrete message type.");


        }
        catch (ExecutionAbortedException)
        {
            // Stopped before a single item existed. Nothing else runs, the final stage
            // included, and the signal reaches whoever is enumerating.
            _executionAborted = true;
            throw;
        }
        catch (Exception exception) when (exception is not ExecutionAbortedException)
        {
            // The stream never came to be, so there is nothing to enumerate; the failure
            // goes straight to the stages below.
            _consume = false;
            _unknownException = exception;
        }



        enumerable ??= Empty<TResult>();
        await using var enumerator = enumerable.GetAsyncEnumerator(cancellationToken);
        while (_consume)
        {
            var item = default(TResult)!;
            try
            {
                _consume = await enumerator.MoveNextAsync().ConfigureAwait(false);
                item = _consume ? enumerator.Current : default!;
            }
            catch (ExecutionAbortedException)
            {
                // Stopped mid-stream: the items already yielded stand, nothing further is
                // produced, and the signal reaches the enumerating caller.
                _executionAborted = true;
                throw;
            }
            catch (Exception exception) when (exception is not ExecutionAbortedException)
            {
                _consume = false;
                _unknownException = exception;
            }
            // Whether MoveNextAsync produced an item is the only thing that decides this:
            // null is a legitimate element of an IAsyncEnumerable<T?>, so testing the item
            // itself would drop it and hand the caller a shorter, well-formed sequence.
            if (_consume && _unknownException is null)
                yield return item;
            if (!_consume || _unknownException is not null)
            {

                break; // exit loop to run post-interceptors
            }
        }
        try
        {
            if (_unknownException is null)
            {
                // The stage receives the enumerator rather than a result: the items are
                // already with the caller, so there is nothing for an interceptor to
                // replace.
                var (_, postCarried) = await PostInterceptorInvocationStrategy<TMessage, IAsyncEnumerator<TResult>>.Invoke(
                    messageDependencies, Results.ResultAdapterBinding.For<TMessage, IAsyncEnumerator<TResult>>(serviceProvider), serviceProvider, message, enumerator, context).ConfigureAwait(false);

                // A failure carried inside a post-interceptor's result reaches the exception
                // stage below without anything being thrown.
                _unknownException = postCarried;
            }
        }
        catch (ExecutionAbortedException)
        {
            _executionAborted = true;
            throw;
        }
        catch (Exception exception) when (exception is not ExecutionAbortedException)
        {
            _unknownException = exception;
        }
        try
        {
            if (_unknownException is not null)
            {
                var (matched, _) = await ExceptionInterceptorInvocationStrategy<TMessage, IAsyncEnumerator<TResult>>.Invoke(
                    messageDependencies, serviceProvider, message, enumerator,
                    _unknownException, context).ConfigureAwait(false);

                if (!matched)
                {
                    // Nobody accepted the failure, so it reaches the caller with its
                    // original stack rather than one rooted here.
                    ExceptionDispatchInfo.Capture(_unknownException).Throw();
                }
            }
        }
        catch (Exception e) when (e is not ExecutionAbortedException)
        {
            throw;
        }
        finally
        {
            if (!_executionAborted)
            {
                await FinalInterceptorInvocationStrategy<TMessage, IAsyncEnumerator<TResult>>.Invoke(
                    messageDependencies, serviceProvider, message, enumerator, _unknownException, context);
            }
        }
    }


    /// <summary>
    /// Returns a sequence with no elements.
    /// </summary>
    /// <typeparam name="T">The element type of the sequence.</typeparam>
    /// <returns>An empty asynchronous sequence.</returns>
    /// <remarks>
    /// Stands in for the stream when the handler never produced one, so the enumeration
    /// below needs no null check.
    /// </remarks>
    #pragma warning disable CS1998
    private static async IAsyncEnumerable<T> Empty<T>()
    #pragma warning restore
    {
        yield break;
    }
}



