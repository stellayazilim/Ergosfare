using Ergosfare.Contracts;
using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Registry.Descriptors;
using Ergosfare.Core.Internal.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Ergosfare.Core.Internal.Mediator;

internal sealed class MessageDependencies : IMessageDependencies
{
    private readonly Type _messageType;
    public ILazyHandlerCollection<IHandler, IMainHandlerDescriptor> Handlers { get; }
    public ILazyHandlerCollection<IHandler, IMainHandlerDescriptor> IndirectHandlers { get; }
    public MessageDependencies(Type messageType,
        IMessageDescriptor descriptor,
        IServiceProvider serviceProvider)
    {
        _messageType = messageType;
        Handlers = ResolveHandlers(descriptor.Handlers, handlerType => (IHandler) serviceProvider.GetRequiredService(handlerType));
        IndirectHandlers = ResolveHandlers(descriptor.IndirectHandlers, handlerType => (IHandler) serviceProvider.GetRequiredService(handlerType));

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
