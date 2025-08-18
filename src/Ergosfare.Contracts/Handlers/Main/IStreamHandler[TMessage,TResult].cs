using Ergosfare.Core.Context;

namespace Ergosfare.Contracts;

public interface IStreamHandler<in TMessage, out TResult>
    :IHandler<TMessage, IAsyncEnumerable<TResult>>
        where TMessage : notnull
{
    IAsyncEnumerable<TResult> IHandler<TMessage, IAsyncEnumerable<TResult>>.Handle(
        TMessage message, 
        IExecutionContext context)
    {
        return StreamAsync(message, context, AmbientExecutionContext.Current.CancellationToken);
    }
    
    IAsyncEnumerable<TResult> StreamAsync(TMessage message,IExecutionContext context, CancellationToken cancellationToken = default);
}