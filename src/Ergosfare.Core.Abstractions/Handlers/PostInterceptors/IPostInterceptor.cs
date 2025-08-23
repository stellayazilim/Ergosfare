using Ergosfare.Context;

namespace Ergosfare.Core.Abstractions.Handlers;
public interface IPostInterceptor
{
    object Handle(object message, object? messageResult, IExecutionContext context);
}