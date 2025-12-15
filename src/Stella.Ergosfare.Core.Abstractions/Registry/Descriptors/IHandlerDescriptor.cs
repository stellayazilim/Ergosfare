using System;
using System.Collections.Generic;

namespace Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;


/// <summary>
/// Represents metadata about a handler for a specific message type.
/// </summary>
/// <remarks>
/// Handler descriptors provide information about:
/// <list type="bullet">
/// <item>Weight: determines the order of execution relative to other handlers.</item>
/// <item>Groups: execution groups that this handler belongs to.</item>
/// <item>HandlerType: the concrete type of the handler.</item>
/// </list>
/// Inherits from <see cref="IHasMessageType"/> to expose the associated message type.
/// </remarks>
public interface IHandlerDescriptor: IHasMessageType
{
    /// <summary>
    /// Gets the weight of the handler, used for ordering execution.
    /// </summary>
    uint Weight { get; }
    
    /// <summary>
    /// Gets the collection of execution groups this handler belongs to.
    /// </summary>
    IReadOnlyCollection<string> Groups { get; }
    
    /// <summary>
    /// Gets the concrete type of the handler.
    /// </summary>
    Type HandlerType { get; }
}