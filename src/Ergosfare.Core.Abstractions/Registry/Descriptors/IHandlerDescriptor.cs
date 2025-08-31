using System;
using System.Collections.Generic;

namespace Ergosfare.Core.Abstractions.Registry.Descriptors;

public interface IHandlerDescriptor: IHasMessageType
{
    uint Weight { get; }
    IReadOnlyCollection<string> Groups { get; }
    Type HandlerType { get; }
}