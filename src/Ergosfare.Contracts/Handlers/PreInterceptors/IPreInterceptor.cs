using Ergosfare.Core.Context;

namespace Ergosfare.Contracts;

public interface IPreInterceptor
{
    object Handle(object message, IExecutionContext context) ;
}