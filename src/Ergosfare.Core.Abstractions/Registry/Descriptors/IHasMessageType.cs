using System;

namespace Ergosfare.Core.Abstractions.Registry.Descriptors;

public interface IHasMessageType
{
    Type MessageType { get; }
}