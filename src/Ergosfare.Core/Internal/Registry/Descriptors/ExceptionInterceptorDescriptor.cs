using Ergosfare.Core.Abstractions.Registry.Descriptors;

namespace Ergosfare.Core.Internal.Registry.Descriptors;


/// <summary>
/// Descriptor for an exception interceptor handler.
/// </summary>
/// <remarks>
/// Inherits from <see cref="HandlerDescriptor"/> and implements <see cref="IExceptionInterceptorDescriptor"/>.
/// Used to describe handlers that intercept exceptions during message handling.
/// </remarks>
internal sealed class ExceptionInterceptorDescriptor: HandlerDescriptor, IExceptionInterceptorDescriptor
{
    
    /// <summary>
    /// Gets or sets the result type that this exception interceptor is associated with.
    /// </summary>
    /// <remarks>
    /// This property is required and must be initialized when creating an instance.
    /// It allows the mediator to understand the type of result the exception interceptor
    /// can handle or transform.
    /// </remarks>
    public required Type ResultType { get; init; }
}