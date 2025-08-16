using System;
using System.Collections.Generic;

namespace Ergosfare.Core.Abstractions.Registry.Descriptors;

public interface IMessageDescriptor
{
  
    Type MessageType { get; }

    bool IsGeneric { get; }

    IReadOnlyCollection<IMainHandlerDescriptor> Handlers  { get; }
    
    IReadOnlyCollection<IMainHandlerDescriptor> IndirectHandlers { get; }

}