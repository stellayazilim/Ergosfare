using Ergosfare.Messaging.Abstractions.Registry.Descriptors;

namespace Ergosfare.Messaging.Abstractions;

public interface IMessageDependencies
{
    ILazyHandlerCollection<IHandler, IMainHandlerDescriptor> Handlers { get; }

}