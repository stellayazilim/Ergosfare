using Ergosfare.Core.Abstractions.Registry.Descriptors;

namespace Ergosfare.Core.Internal.Registry.Descriptors;

/// <summary>
/// Descriptor for a main handler that handles a specific message type and produces a result.
/// </summary>
/// <remarks>
/// Inherits from <see cref="HandlerDescriptor"/> and adds the <see cref="ResultType"/> property
/// to specify the type of result produced by the handler.
/// </remarks>
internal sealed class MainHandlerDescriptor : HandlerDescriptor, IMainHandlerDescriptor
{
    /// <summary>
    /// Gets the type of the result produced by the handler.
    /// </summary>
    public required Type ResultType { get; init; }
}