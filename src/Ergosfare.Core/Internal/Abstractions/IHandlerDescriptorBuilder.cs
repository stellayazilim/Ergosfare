using Ergosfare.Core.Abstractions.Registry.Descriptors;

namespace Ergosfare.Core.Internal.Abstractions;

internal interface IHandlerDescriptorBuilder
{
    bool CanBuild(Type type);
    
    IHandlerDescriptor Build(Type type);
}