using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using Ergosfare.Core.Abstractions.Exceptions;
using Ergosfare.Core.Abstractions.Strategies.InvocationStrategies;

namespace Ergosfare.Core.Abstractions.Strategies;


/// <summary>
/// Implements a mediation strategy for a single asynchronous streaming handler.
/// Ensures that only one handler is executed for the message, invokes pre- and post-interceptors,
/// handles exceptions, and applies final interceptors. Supports chunked streaming results with optional result adaptation.
/// </summary>
/// <typeparam name="TMessage">The type of the message being handled.</typeparam>
/// <typeparam name="TResult">The type of the elements returned by the asynchronous stream.</typeparam>
public sealed class SingleStreamHandlerMediationStrategy<TMessage, TResult>( 
    IResultAdapterService? resultAdapterService,
    CancellationToken cancellationToken) : IMessageMediationStrategy<TMessage, IAsyncEnumerable<TResult>>
    where TMessage : notnull
{
        
    // an unknown exception occurred that Ergosfare doesn't recognize through pipeline
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
    /// <param name="executionContext">The current execution context.</param>
    /// <returns>
    /// An <see cref="IAsyncEnumerable{TResult}"/> representing the asynchronous stream of results produced by the handler.
    /// </returns>
    /// <exception cref="MultipleHandlerFoundException">Thrown if more than one handler is registered for the message.</exception>
    public async IAsyncEnumerable<TResult> Mediate(TMessage message, IMessageDependencies messageDependencies,
        IExecutionContext context)
    {
        if (messageDependencies.Handlers.Count > 1)
        {
            throw new MultipleHandlerFoundException(typeof(TMessage), messageDependencies.Handlers.Count);
        }
        
        AmbientExecutionContext.Current = context;
        var handler = messageDependencies
            .Handlers
            .Single()
            .Handler
            .Value;
        
        
        if (handler is null)
        {
            throw new InvalidOperationException(
                $"Handler for {typeof(TMessage).Name} is not of the expected type.");
        }
        

        
        
        // enumerator to consume
        IAsyncEnumerable<TResult>? enumerable = null;

        try
        {
            // run pre interceptors
            var preInvoker = new TaskPreInterceptorInvocationStrategy(messageDependencies, resultAdapterService);
            message =  (TMessage)await preInvoker.Invoke(message, context) ;



            enumerable = (IAsyncEnumerable<TResult>?)handler.Handle(message, context);
           

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
                var postInvoker = new TaskPostInterceptorInvocationStrategy(messageDependencies, resultAdapterService);
                // we can't override result since its chunked
                await postInvoker.Invoke(message, enumerator, context).ConfigureAwait(false);
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
                var exceptionInvoker = new TaskExceptionInterceptorInvocationStrategy(messageDependencies, resultAdapterService);
                // we can't override result since its chunked
                await exceptionInvoker.Invoke(
                    message,
                    enumerator,
                    ExceptionDispatchInfo.Capture(_unknownException),
                    context).ConfigureAwait(false);
            }
        }
        catch (Exception e) when (e is not ExecutionAbortedException)
        {
            throw;
        }
        finally
        {
            var finalInvoker = new TaskFinalInterceptorInvocationStrategy(messageDependencies, resultAdapterService);
            await finalInvoker.Invoke(message, enumerator, _unknownException, context);
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



