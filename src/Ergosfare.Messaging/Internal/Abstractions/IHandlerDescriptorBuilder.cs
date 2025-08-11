using Ergosfare.Messaging.Abstractions.Registry.Descriptors;

namespace Ergosfare.Messaging.Internal.Abstractions;

internal interface IHandlerDescriptorBuilder
{
    bool CanBuild(Type type);
    
    IHandlerDescriptor Build(Type type);
}