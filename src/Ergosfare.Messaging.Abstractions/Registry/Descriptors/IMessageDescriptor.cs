using System;

namespace Ergosfare.Messaging.Abstractions.Registry.Descriptors;

public interface IMessageDescriptor
{
  
    Type MessageType { get; }

    bool IsGeneric { get; }

    IMainHandlerDescriptor Handler  { get; }
}