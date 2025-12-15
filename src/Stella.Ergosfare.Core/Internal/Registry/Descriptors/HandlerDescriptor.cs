using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;

namespace Stella.Ergosfare.Core.Internal.Registry.Descriptors;


/// <summary>
/// Base descriptor for a handler.
/// </summary>
/// <remarks>
/// Provides common metadata for all handler types including weight, groups, message type, and handler type.
/// Concrete descriptors (e.g., <see cref="ExceptionInterceptorDescriptor"/> or <see cref="FinalInterceptorDescriptor"/>) inherit from this class.
/// </remarks>
internal abstract class HandlerDescriptor: IHandlerDescriptor
{
    /// <summary>
    /// Gets or sets the execution priority of the handler.
    /// Handlers with higher weight are executed first.
    /// </summary>
    public uint Weight { get; init; }
    
    /// <summary>
    /// Gets the groups this handler belongs to.
    /// Handlers are filtered by group names during mediation.
    /// </summary>
    public required IReadOnlyCollection<string> Groups { get; init; }
    
    /// <summary>
    /// Gets the type of the message this handler handles.
    /// </summary>
    public required Type MessageType { get; init;  }

    /// <summary>
    /// Gets the concrete type of the handler implementation.
    /// </summary>
    public required Type HandlerType { get; init; }
}