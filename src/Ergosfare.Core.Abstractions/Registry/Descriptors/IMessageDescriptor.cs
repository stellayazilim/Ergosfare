using System.Collections.Generic;

namespace Ergosfare.Core.Abstractions.Registry.Descriptors;

public interface IMessageDescriptor: IHasMessageType
{
    bool IsGeneric { get; }

    IReadOnlyCollection<IMainHandlerDescriptor> Handlers  { get; }
    
    IReadOnlyCollection<IMainHandlerDescriptor> IndirectHandlers { get; }

}