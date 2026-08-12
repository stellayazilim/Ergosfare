using System.Runtime.ExceptionServices;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Abstractions.Strategies.InvocationStrategies;

namespace Stella.Ergosfare.Core.Abstractions.Strategies;


/// <summary>
/// Implements a mediation strategy for a single asynchronous streaming handler.
/// Ensures that only one handler is executed for the message, invokes pre- and post-interceptors,
/// handles exceptions, and applies final interceptors. Supports chunked streaming results with optional result adaptation.
/// </summary>
/// <typeparam name="TMessage">The type of the message being handled.</typeparam>
/// <typeparam name="TResult">The type of the elements returned by the asynchronous stream.</typeparam>
public sealed class SingleStreamHandlerMediationStrategy<TMessage, TResult>( 
    CancellationToken cancellationToken) : IMessageMediationStrategy<TMessage, IAsyncEnumerable<TResult>>
    where TMessage : notnull
{
        
    // an unknown exception occurred that Stella.Ergosfare doesn't recognize through pipeline
    Exception? _unknownException;
        
    // is execution aborted at any path of pipeline
    bool _executionAborted;
        
    // async enumerable still consumable, ie all _consumed, exception happened
    bool _consume = true;
    
    
    /// <summary>
    /// Mediates the message by invoking the streaming handler along with pre-, post-, exception-, and final interceptors.
    /// Supports chunked streaming results with early abortion or exception handling.
    /// </summary>
    /// <param name="message">The message to be handled.</param>
    /// <param name="messageDependencies">The dependencies of the message, including the registered handlers and interceptors.</param>
    /// <param name="context">The current execution context.</param>
    /// <param name="serviceProvider">The provider of the scope this dispatch runs in; handlers and interceptors resolve from it.</param>
    /// <returns>
    /// An <see cref="IAsyncEnumerable{TResult}"/> representing the asynchronous stream of results produced by the handler.
    /// </returns>
    /// <exception cref="MultipleHandlerFoundException">Thrown if more than one handler is registered for the message.</exception>
    /// <exception cref="NoHandlerFoundException">Thrown if no handler is registered for the message.</exception>
    public async IAsyncEnumerable<TResult> Mediate(TMessage message, IMessageDependencies messageDependencies,
        ErgosfareContext context, IServiceProvider serviceProvider)
    {
        // The main-handler priority ladder; see SingleAsyncHandlerMediationStrategy{TMessage}
        // for the reasoning.
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

        // enumerator to consume
        IAsyncEnumerable<TResult>? enumerable = null;

        try
        {
            // run pre interceptors
            message = (TMessage) await PreInterceptorInvocationStrategy<TMessage>.Invoke(
                messageDependencies, serviceProvider, message, context);


            // Typed dispatch only — no object bridge. `in TMessage` variance admits handlers
            // registered for base message types; IAsyncEnumerable<out T> covariance admits
            // derived elements.
            enumerable = handler is IHandler<TMessage, IAsyncEnumerable<TResult>> typed
                ? typed.Handle(message, context)
                : throw new NotSupportedException(
                    $"'{handler.GetType()}' does not implement a supported stream handler contract for message '{typeof(TMessage)}'. " +
                    "Interface-erased dispatch is not supported; dispatch with the concrete message type.");
           

        }
        catch (ExecutionAbortedException)
        {
            // aborted early no need to _consume
            _consume = false;
            _executionAborted = true;

        }
        catch (Exception exception) when (exception is not ExecutionAbortedException)
        {
            // exception happened no need to _consume
            _consume = false;
            _unknownException = exception;
        }

     

        enumerable ??= Empty<TResult>();
        await using var enumerator = enumerable.GetAsyncEnumerator(cancellationToken);
        while (_consume)
        {
            TResult? item = default;
            try
            {
                _consume = await enumerator.MoveNextAsync().ConfigureAwait(false);
                item = _consume ? enumerator.Current : default;
            }
            catch (ExecutionAbortedException)
            {
                _consume = false;
                _executionAborted = true;
                
            }
            catch (Exception exception) when (exception is not ExecutionAbortedException)
            {
                _consume = false;
                _unknownException = exception;
            }
            if (item is not null && _consume && _unknownException is null && !_executionAborted)
                yield return item;
            if (!_consume || _unknownException is not null)
            {
          
                break; // exit loop to run post-interceptors
            }

            if (_executionAborted)
                yield break; // stop pipeline early
        }
        try
        {
            if (_unknownException is null)
            {
                // we can't override result since its chunked
                var (_, postCarried) = await PostInterceptorInvocationStrategy<TMessage, IAsyncEnumerator<TResult>>.Invoke(
                    messageDependencies, Results.ResultAdapterBinding.For<TMessage, IAsyncEnumerator<TResult>>(serviceProvider), serviceProvider, message, enumerator, context).ConfigureAwait(false);

                // A failure carried inside a post result enters the exception stage below
                // without a throw — the stream's value channel.
                _unknownException = postCarried;
            }
        }
        catch (ExecutionAbortedException)
        { /*all chunks _consumed no action need*/ }
        catch (Exception exception) when (exception is not ExecutionAbortedException)
        { 
            _unknownException = exception;
        }
        try
        {
            if (_unknownException is not null)
            {
                // we can't override result since its chunked
                var (matched, _) = await ExceptionInterceptorInvocationStrategy<TMessage, IAsyncEnumerator<TResult>>.Invoke(
                    messageDependencies, serviceProvider, message, enumerator,
                    _unknownException, context).ConfigureAwait(false);

                if (!matched)
                {
                    // Nobody accepted the failure: it surfaces with its original stack,
                    // exactly as the stage's own rethrow used to.
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
            await FinalInterceptorInvocationStrategy<TMessage, IAsyncEnumerator<TResult>>.Invoke(
                messageDependencies, serviceProvider, message, enumerator, _unknownException, context);
        }
    }
    
    
    /// <summary>
    /// Returns an empty asynchronous sequence of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of elements in the sequence.</typeparam>
    /// <returns>An <see cref="IAsyncEnumerable{T}"/> that contains no elements.</returns>
    /// <remarks>
    /// This method is used internally to provide an empty async enumerable when no data is available,
    /// avoiding null checks for asynchronous iteration.
    /// </remarks>
    #pragma warning disable CS1998 
    private static async IAsyncEnumerable<T> Empty<T>()
    #pragma warning restore 
    {
        yield break;
    }
}



