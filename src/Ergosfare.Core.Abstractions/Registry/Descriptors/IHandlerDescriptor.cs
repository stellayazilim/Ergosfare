using System;

namespace Ergosfare.Core.Abstractions.Registry.Descriptors;

public interface IHandlerDescriptor
{
    Type MessageType { get; }
    Type HandlerType { get; }
}