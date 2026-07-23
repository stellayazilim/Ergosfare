using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// A descriptor paired with the concrete handler type to resolve for it, computed once
/// when the pipeline shape is built and reused for every dispatch afterward.
/// </summary>
/// <remarks>
/// For handlers of generic messages the type is already closed over the message's generic
/// arguments here, so dispatches never pay for <see cref="Type.MakeGenericType"/> again.
/// </remarks>
internal readonly struct PlannedHandler<TDescriptor> where TDescriptor : IHandlerDescriptor
{
    /// <summary>
    /// The handler descriptor as registered.
    /// </summary>
    public required TDescriptor Descriptor { get; init; }

    /// <summary>
    /// The concrete type to request from the service provider.
    /// </summary>
    public required Type HandlerType { get; init; }
}
