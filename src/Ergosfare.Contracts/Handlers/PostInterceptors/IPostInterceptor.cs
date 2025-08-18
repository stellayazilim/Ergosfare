using Ergosfare.Core.Context;

namespace Ergosfare.Contracts;

public interface IPostInterceptor
{
    object Handle(object message, object? messageResult, IExecutionContext context);
}