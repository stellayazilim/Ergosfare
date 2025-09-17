using System;

namespace Ergosfare.Core.Abstractions.Registry.Descriptors;


/// <summary>
/// Represents an object that produces or is associated with a specific result type.
/// </summary>
/// <remarks>
/// This interface is typically implemented by handler descriptors or interceptors
/// that have a specific <see cref="ResultType"/> for the output of their operation.
/// </remarks>
public interface IHasResultType
{
    /// <summary>
    /// Gets the <see cref="Type"/> of the result produced or handled by this object.
    /// </summary>
    Type ResultType { get; }
}