using System;

namespace Ergosfare.Messaging.Abstractions.Registry.Descriptors;

public interface IHandlerDescriptor
{
    Type MessageType { get; }
    Type HandlerType { get; }
}