using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using Ergosfare.Core.Abstractions.Exceptions;
using Ergosfare.Core.Abstractions.Extensions;
using Ergosfare.Context;

namespace Ergosfare.Core.Abstractions.Strategies;


public sealed class SingleStreamHandlerMediationStrategy<TMessage, TResult>( 
    IResultAdapterService? resultAdapterService,
    CancellationToken cancellationToken) : IMessageMediationStrategy<TMessage, IAsyncEnumerable<TResult>>
    where TMessage : notnull
{
    
    private readonly CancellationToken _cancellationToken = cancellationToken;
    

        
    // an unknown exception occoured that Ergosfare doesn't recognize through pipeline
    Exception? _unknownException = null;
        
    // is excecution aborted at any path of pipeline
    bool _executionAborted = false;
        
    // async enumerable still consumable, ie all _consumed, exception happened
    bool _consume = true;
    
    public async IAsyncEnumerable<TResult> Mediate(TMessage message, IMessageDependencies messageDependencies,
        IExecutionContext executionContext)
    {
        if (messageDependencies.Handlers.Count > 1)
        {
            throw new MultipleHandlerFoundException(typeof(TMessage), messageDependencies.Handlers.Count);
        }
        
        AmbientExecutionContext.Current = executionContext; 
        
        // enumerator to consume
        var enumerable = ((IAsyncEnumerable<TResult>)messageDependencies
            .Handlers
            .Single()
            .Handler
            .Value
            .Handle(message, executionContext));

        
        // run pre interceptors

        try
        {
            await messageDependencies.RunAsyncPreInterceptors(message, executionContext).ConfigureAwait(false);
        }
        catch (ExecutionAbortedException e)
        {
            // aborted early no need to _consume
            _consume = false;
            _executionAborted = true;

        }
        catch (Exception exception) when (exception is not ExecutionAbortedException)
        {
            // exception happaned no need to _consume
            _consume = false;
            _unknownException = exception;
        }

     

        enumerable ??= Empty<TResult>();
        await using var enumerator = enumerable.GetAsyncEnumerator(_cancellationToken);
        
        
        while (_consume)
        {

            TResult? item = default;
            try
            {
                _consume = await enumerator.MoveNextAsync().ConfigureAwait(false);
                item = _consume ? enumerator.Current : default;
            }
            catch (ExecutionAbortedException e)
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
                break; // exit loop to run post-interceptors

            if (_executionAborted)
                yield break; // stop pipeline early
        }


        try
        {
            if (_unknownException is null)
                await messageDependencies.RunAsyncPostInterceptors(message, enumerator, executionContext, resultAdapterService).ConfigureAwait(false);
        }
        catch (ExecutionAbortedException e)
        { /*all chunks _consumed no action need*/ }
        catch (Exception exception) when (exception is not ExecutionAbortedException)
        { 
            _unknownException = exception;
        }



        try
        {
            if (_unknownException is not null)
            {
                await messageDependencies.RunAsyncExceptionInterceptors(
                    message,
                    enumerator,
                    ExceptionDispatchInfo.Capture(_unknownException),
                    executionContext).ConfigureAwait(false);

            }
        }
        catch (Exception e) when (e is not ExecutionAbortedException)
        {
            throw;
        }

        finally
        {
            await messageDependencies.RunAsyncFinalInterceptors(message, enumerator, _unknownException, executionContext);
        }
    }
    
    

    #pragma warning disable CS1998 
    private static async IAsyncEnumerable<T> Empty<T>()
    #pragma warning restore 
    {
        yield break;
    }
}



