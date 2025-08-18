using Ergosfare.Contracts;
using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Registry.Descriptors;
using Ergosfare.Core.Internal.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Ergosfare.Core.Internal.Mediator;

internal sealed class MessageDependencies : IMessageDependencies
{
    private readonly Type _messageType;
    
    
    public ILazyHandlerCollection<IPreInterceptor, IPreInterceptorDescriptor> PreInterceptors { get; }
    
    public ILazyHandlerCollection<IPreInterceptor, IPreInterceptorDescriptor> IndirectPreInterceptors { get; }
    
    
    public ILazyHandlerCollection<IHandler, IMainHandlerDescriptor> Handlers { get; }
    public ILazyHandlerCollection<IHandler, IMainHandlerDescriptor> IndirectHandlers { get; }
    

    public ILazyHandlerCollection<IPostInterceptor, IPostInterceptorDescriptor> PostInterceptors { get; }
    
    public ILazyHandlerCollection<IPostInterceptor, IPostInterceptorDescriptor> IndirectPostInterceptors { get; }
    
    public MessageDependencies(Type messageType,
        IMessageDescriptor descriptor,
        IServiceProvider serviceProvider)
    {
        _messageType = messageType;
        
        // resolve pre interceptors
        PreInterceptors = ResolveHandlers(
            descriptor.PreInterceptors, 
            handlerType => (IPreInterceptor) serviceProvider.GetRequiredService(handlerType));
        
        IndirectPreInterceptors = ResolveHandlers(
            descriptor.IndirectPreInterceptors, 
            handlerType => (IPreInterceptor) serviceProvider.GetRequiredService(handlerType));

        
        // resolve main handlers
        Handlers = ResolveHandlers(descriptor.Handlers, handlerType => (IHandler) serviceProvider.GetRequiredService(handlerType));
        IndirectHandlers = ResolveHandlers(descriptor.IndirectHandlers, handlerType => (IHandler) serviceProvider.GetRequiredService(handlerType));

        // resolve post interceptors
        PostInterceptors = ResolveHandlers(
            descriptor.PostInterceptors,
            handlerType => (IPostInterceptor)  serviceProvider.GetRequiredService(handlerType));
        
        IndirectPostInterceptors = ResolveHandlers(
            descriptor.IndirectPostInterceptors,
            handlerType => (IPostInterceptor)  serviceProvider.GetRequiredService(handlerType));
       
    }

    private ILazyHandlerCollection<THandler, TDescriptor> ResolveHandlers<THandler, TDescriptor>(
            IEnumerable<TDescriptor> descriptors, 
            Func<Type, THandler> resolveFunc ) where TDescriptor : IHandlerDescriptor
    {
        return descriptors
            .Select(d => new LazyHandler<THandler, TDescriptor>
            {
                Handler = new Lazy<THandler>(() => resolveFunc(GetHandlerType(d))),
                Descriptor = d
            })
            .ToLazyReadOnlyCollection();
    }

    private Type GetHandlerType(IHandlerDescriptor descriptor)
    {
        var handlerType = descriptor.HandlerType;

        if (descriptor.MessageType.IsGenericType)
        {
            handlerType = handlerType.MakeGenericType(_messageType.GetGenericArguments());
        }

        return handlerType;
    }
    
}
