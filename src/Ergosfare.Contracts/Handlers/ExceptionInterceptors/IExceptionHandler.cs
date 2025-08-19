using Ergosfare.Core.Context;

namespace Ergosfare.Contracts;

public interface IExceptionInterceptor
{
    object Handle(object message,  object? messageResult, Exception exception,  IExecutionContext context);
}