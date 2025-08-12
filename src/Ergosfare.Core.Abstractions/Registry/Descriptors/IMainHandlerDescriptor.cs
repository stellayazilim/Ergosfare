using System;

namespace Ergosfare.Core.Abstractions.Registry.Descriptors;

public interface  IMainHandlerDescriptor: IHandlerDescriptor
{
    Type ResultType { get; }
}