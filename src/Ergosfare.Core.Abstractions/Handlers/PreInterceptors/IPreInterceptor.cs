using Ergosfare.Context;

namespace Ergosfare.Core.Abstractions.Handlers;
public interface IPreInterceptor
{
    object Handle(object message, IExecutionContext context) ;
}