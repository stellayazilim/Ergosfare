using System;

namespace Ergosfare.Core.Abstractions.Registry.Descriptors;

public interface IHandlerDescriptor: IHasMessageType
{
    Type HandlerType { get; }
}