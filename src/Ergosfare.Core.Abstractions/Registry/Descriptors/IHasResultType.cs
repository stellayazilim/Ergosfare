using System;

namespace Ergosfare.Core.Abstractions.Registry.Descriptors;

public interface IHasResultType
{
    Type ResultType { get; }
}