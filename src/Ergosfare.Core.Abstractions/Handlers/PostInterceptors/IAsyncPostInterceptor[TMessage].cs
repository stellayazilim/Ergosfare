using System.Threading;
using System.Threading.Tasks;
using Ergosfare.Context;

namespace Ergosfare.Core.Abstractions.Handlers;
public interface IAsyncPostInterceptor<in TMessage>
    : IPostInterceptor<TMessage, object>
    where TMessage : notnull
{
    Task HandleAsync(TMessage message, object? messageResult, IExecutionContext context, CancellationToken cancellationToken = default);
}