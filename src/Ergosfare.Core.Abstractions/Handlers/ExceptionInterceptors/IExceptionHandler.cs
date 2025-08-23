using System;
using Ergosfare.Context;

namespace Ergosfare.Core.Abstractions.Handlers;
public interface IExceptionInterceptor
{
    object Handle(object message,  object? messageResult, Exception exception,  IExecutionContext context);
}