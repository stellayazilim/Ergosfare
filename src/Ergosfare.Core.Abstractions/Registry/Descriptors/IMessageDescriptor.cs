using System.Collections.Generic;

namespace Ergosfare.Core.Abstractions.Registry.Descriptors;

public interface IMessageDescriptor: IHasMessageType
{
    bool IsGeneric { get; }

    IReadOnlyCollection<IMainHandlerDescriptor> Handlers  { get; }
    
    IReadOnlyCollection<IMainHandlerDescriptor> IndirectHandlers { get; }
    
    IReadOnlyCollection<IPreInterceptorDescriptor> PreInterceptors { get; }
    IReadOnlyCollection<IPreInterceptorDescriptor> IndirectPreInterceptors { get; }
    
    IReadOnlyCollection<IPostInterceptorDescriptor> PostInterceptors { get; }
    IReadOnlyCollection<IPostInterceptorDescriptor> IndirectPostInterceptors { get; }
    
    IReadOnlyCollection<IExceptionInterceptorDescriptor> ExceptionInterceptors { get; }
    IReadOnlyCollection<IExceptionInterceptorDescriptor> IndirectExceptionInterceptors { get; }
}