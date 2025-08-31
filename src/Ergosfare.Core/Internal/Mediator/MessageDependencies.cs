using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Handlers;
using Ergosfare.Core.Abstractions.Registry.Descriptors;
using Ergosfare.Core.Internal.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Ergosfare.Core.Internal.Mediator;

internal sealed class MessageDependencies : IMessageDependencies
{
    private readonly Type _messageType;
    
    private readonly IEnumerable<string> _groups;
    
    public ILazyHandlerCollection<IPreInterceptor, IPreInterceptorDescriptor> PreInterceptors { get; }
    public ILazyHandlerCollection<IPreInterceptor, IPreInterceptorDescriptor> IndirectPreInterceptors { get; }
    
    
    public ILazyHandlerCollection<IHandler, IMainHandlerDescriptor> Handlers { get; }
    public ILazyHandlerCollection<IHandler, IMainHandlerDescriptor> IndirectHandlers { get; }
    

    public ILazyHandlerCollection<IPostInterceptor, IPostInterceptorDescriptor> PostInterceptors { get; }
    public ILazyHandlerCollection<IPostInterceptor, IPostInterceptorDescriptor> IndirectPostInterceptors { get; }
  
    
    public ILazyHandlerCollection<IExceptionInterceptor, IExceptionInterceptorDescriptor> ExceptionInterceptors { get; }
    public ILazyHandlerCollection<IExceptionInterceptor, IExceptionInterceptorDescriptor> IndirectExceptionInterceptors { get; }
    
    
    #pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
  
    #pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    public MessageDependencies(Type messageType,
        IMessageDescriptor descriptor,
        IServiceProvider serviceProvider,
        IEnumerable<string> groups)
    {
        _messageType = messageType;
        
        _groups = groups;

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
        
        
        // resolve exception interceptors
        ExceptionInterceptors = ResolveHandlers(
            descriptor.ExceptionInterceptors,
            handlerType => (IExceptionInterceptor)  serviceProvider.GetRequiredService(handlerType)
            );
        
        IndirectExceptionInterceptors = ResolveHandlers(
            descriptor.IndirectExceptionInterceptors,
            handlerType => (IExceptionInterceptor)  serviceProvider.GetRequiredService(handlerType));
    }

    private ILazyHandlerCollection<THandler, TDescriptor> ResolveHandlers<THandler, TDescriptor>(
            IEnumerable<TDescriptor> descriptors, 
            Func<Type, THandler> resolveFunc ) where TDescriptor : IHandlerDescriptor
    {
        return descriptors
            .OrderByDescending(d => d.Weight)
            .Where(d => d.Groups.Count == 0 || d.Groups.Intersect(_groups).Any())
            .Select<TDescriptor, ILazyHandler<THandler, TDescriptor>>(d => new LazyHandler<THandler, TDescriptor>
            {
                Handler = new Lazy<THandler>(() => resolveFunc(GetHandlerType(d))),
                Descriptor = d
            })
            .ToLazyReadOnlyCollection<THandler, TDescriptor>();
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
