using System.Collections.Generic;
using System.Threading;
using Ergosfare.Context;

namespace Ergosfare.Core.Abstractions.Handlers;
public interface IStreamHandler<in TMessage, out TResult>
    :IHandler<TMessage, IAsyncEnumerable<TResult>>
        where TMessage : notnull
{
    IAsyncEnumerable<TResult> IHandler<TMessage, IAsyncEnumerable<TResult>>.Handle(
        TMessage message, 
        IExecutionContext context)
    {
        return StreamAsync(message, context);
    }
    
    IAsyncEnumerable<TResult> StreamAsync(TMessage message,IExecutionContext context);
}