using Ergosfare.Messaging.Abstractions.Registry.Descriptors;

namespace Ergosfare.Messaging.Internal.Registry.Descriptors;

internal abstract class HandlerDescriptor: IHandlerDescriptor
{
    public required Type MessageType { get; init;  }
    public required Type HandlerType { get; init; }
}