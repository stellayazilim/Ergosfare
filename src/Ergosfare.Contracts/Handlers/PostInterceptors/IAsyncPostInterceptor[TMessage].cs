using Ergosfare.Core.Context;

namespace Ergosfare.Contracts;

public interface IAsyncPostInterceptor<in TMessage>
    : IPostInterceptor<TMessage, object>
    where TMessage : notnull
{
    Task HandleAsync(TMessage message, object? messageResult, IExecutionContext context, CancellationToken cancellationToken = default);
}