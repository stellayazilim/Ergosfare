using System;
using System.Collections.Generic;
using Ergosfare.Core.Abstractions.Registry.Descriptors;

namespace Ergosfare.Core.Abstractions.Registry;

public interface IMessageRegistry : IReadOnlyCollection<IMessageDescriptor>
{

    void Register(Type type);
}