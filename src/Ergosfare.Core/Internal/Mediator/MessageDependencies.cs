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

    public MessageDependencies(Type messageType,
        IMessageDescriptor descriptor,
        IServiceProvider serviceProvider,
        IEnumerable<string> tags)
    {
        _messageType = messageType;

        Handlers = ResolveHandlers(descriptor.Handler, handlerType => (IHandler)serviceProvider.GetRequiredService(handlerType));
    }

    private ILazyHandlerCollection<THandler, TDescriptor> ResolveHandlers<THandler, TDescriptor>(
        TDescriptor descriptor,
        Func<Type, THandler> resolveFunc) where TDescriptor : IHandlerDescriptor
    {
        var lazyHandler = new LazyHandler<THandler, TDescriptor>
        {
            Handler = new Lazy<THandler>(() => resolveFunc(GetHandlerType(descriptor))),
            Descriptor = descriptor
        };

        return new[] { lazyHandler }.ToLazyReadOnlyCollection();
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
