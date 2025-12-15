using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;

namespace Stella.Ergosfare.Core.Internal.Registry.Descriptors;


/// <summary>
/// Descriptor for a final interceptor handler.
/// </summary>
/// <remarks>
/// Inherits from <see cref="HandlerDescriptor"/> and implements <see cref="IFinalInterceptorDescriptor"/>.
/// Represents handlers that execute at the final stage of message handling, typically for cleanup
/// or final processing of results.
/// </remarks>
internal sealed class FinalInterceptorDescriptor: HandlerDescriptor, IFinalInterceptorDescriptor
{
    
    /// <summary>
    /// Gets or sets the result type that this final interceptor is associated with.
    /// </summary>
    /// <remarks>
    /// This property is required and must be initialized when creating an instance.
    /// It allows the mediator to understand the type of result the final interceptor
    /// can handle or transform.
    /// </remarks>
    public required Type ResultType { get; init; }
}