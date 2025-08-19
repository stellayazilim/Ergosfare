using Ergosfare.Core.Abstractions.Registry.Descriptors;
using Ergosfare.Contracts;
namespace Ergosfare.Core.Abstractions;

public interface IMessageDependencies
{
    ILazyHandlerCollection<IHandler, IMainHandlerDescriptor> Handlers { get; } 
    ILazyHandlerCollection<IHandler, IMainHandlerDescriptor> IndirectHandlers { get; }
    ILazyHandlerCollection<IPreInterceptor, IPreInterceptorDescriptor> PreInterceptors { get; }
    ILazyHandlerCollection<IPreInterceptor, IPreInterceptorDescriptor> IndirectPreInterceptors { get; }
    ILazyHandlerCollection<IPostInterceptor, IPostInterceptorDescriptor> PostInterceptors { get; }
    ILazyHandlerCollection<IPostInterceptor, IPostInterceptorDescriptor> IndirectPostInterceptors { get; }
    ILazyHandlerCollection<IExceptionInterceptor, IExceptionInterceptorDescriptor> ExceptionInterceptors { get; }
    ILazyHandlerCollection<IExceptionInterceptor, IExceptionInterceptorDescriptor> IndirectExceptionInterceptors { get; }
}