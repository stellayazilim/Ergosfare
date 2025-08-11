using System;
using System.Collections.Generic;
using Ergosfare.Messaging.Abstractions.Registry.Descriptors;

namespace Ergosfare.Messaging.Abstractions.Registry;

public interface IMessageRegistry : IReadOnlyCollection<IMessageDescriptor>
{

    void Register(Type type);
}