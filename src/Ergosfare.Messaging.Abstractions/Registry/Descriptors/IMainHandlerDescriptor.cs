using System;

namespace Ergosfare.Messaging.Abstractions.Registry.Descriptors;

public interface  IMainHandlerDescriptor: IHandlerDescriptor
{
    Type ResultType { get; }
}