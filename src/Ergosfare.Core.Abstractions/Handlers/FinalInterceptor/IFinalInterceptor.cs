using System;
using System.Runtime.CompilerServices;
using Ergosfare.Context;

namespace Ergosfare.Core.Abstractions.Handlers;

public interface IFinalInterceptor
{
    object Handle(object message, object? result, Exception? exception, IExecutionContext context);
}