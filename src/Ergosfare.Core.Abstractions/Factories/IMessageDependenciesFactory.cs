using System;
using System.Collections.Generic;
using Ergosfare.Core.Abstractions.Registry.Descriptors;

namespace Ergosfare.Core.Abstractions.Factories;

public interface IMessageDependenciesFactory
{
    public IMessageDependencies Create(Type messageType, IMessageDescriptor descriptor);
}