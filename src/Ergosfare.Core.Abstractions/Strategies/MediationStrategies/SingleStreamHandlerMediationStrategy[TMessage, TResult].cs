using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using Ergosfare.Contracts;
using Ergosfare.Core.Abstractions.Exceptions;
using Ergosfare.Core.Abstractions.Extensions;
using Ergosfare.Core.Context;

namespace Ergosfare.Core.Abstractions.Strategies;


public sealed class SingleStreamHandlerMediationStrategy<TMessage, TResult>( CancellationToken cancellationToken) : IMessageMediationStrategy<TMessage, IAsyncEnumerable<TResult>>
    where TMessage : IMessage
{
    
    private readonly CancellationToken _cancellationToken = cancellationToken;
    public async IAsyncEnumerable<TResult> Mediate(TMessage message, IMessageDependencies messageDependencies,
        IExecutionContext executionContext)
    {
        if (messageDependencies.Handlers.Count > 1)
        {
            throw new MultipleHandlerFoundException(typeof(TMessage), messageDependencies.Handlers.Count);
        }
        
        IAsyncEnumerable<TResult>? messageResultAsyncEnumerable = null;
        var shouldContinue = true;

        try
        {
            AmbientExecutionContext.Current = executionContext;


            var handler = messageDependencies.Handlers.Single().Handler.Value;

            messageResultAsyncEnumerable = (IAsyncEnumerable<TResult>) handler.Handle(message, executionContext);
        }      
        catch (ExecutionAbortedException)
        {
            // Execution was aborted during pre-handling, terminate the stream
            shouldContinue = false;
        }
        catch (Exception exception) when (exception is not ExecutionAbortedException)
        {
            await messageDependencies.RunAsyncExceptionInterceptors(message, messageResultAsyncEnumerable, ExceptionDispatchInfo.Capture(exception), executionContext);
        }
        
        if (!shouldContinue)
        {
            yield break;
        }
        
        messageResultAsyncEnumerable ??= Empty<TResult>();
        
        await using var messageResultAsyncEnumerator = messageResultAsyncEnumerable.GetAsyncEnumerator(_cancellationToken);

        TResult? item = default;
        var hasResult = true;

        while (hasResult && shouldContinue)
        {
            try
            {
                hasResult = await messageResultAsyncEnumerator.MoveNextAsync().ConfigureAwait(false);

                item = hasResult ? messageResultAsyncEnumerator.Current : default;
            }
            catch (ExecutionAbortedException)
            {
                shouldContinue = false;
                continue;
            }
            catch (Exception exception) when (exception is not ExecutionAbortedException)
            {
                await messageDependencies.RunAsyncExceptionInterceptors(message, messageResultAsyncEnumerable, ExceptionDispatchInfo.Capture(exception), executionContext);

            }

            if (item != null && hasResult && shouldContinue)
            {
                AmbientExecutionContext.Current = executionContext;
                yield return item;
            }
        }
        
        
        if (!shouldContinue)
        {
            // Stream was terminated early, skip post-handlers
            yield break;
        }

        try
        {
            AmbientExecutionContext.Current = executionContext;
            // Post pipeline processer
        }
        catch (Exception exception) when (exception is not ExecutionAbortedException)
        {
            AmbientExecutionContext.Current = executionContext;
            // error processor
        }
    }
    
    

    #pragma warning disable CS1998 
    private static async IAsyncEnumerable<T> Empty<T>()
    #pragma warning restore 
    {
        yield break;
    }
}