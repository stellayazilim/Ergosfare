using Ergosfare.Messaging.Abstractions.Registry.Descriptors;

namespace Ergosfare.Messaging.Internal.Registry.Descriptors;

internal sealed class MainHandlerDescriptor : HandlerDescriptor, IMainHandlerDescriptor
{
    public required Type ResultType { get; init; }
}