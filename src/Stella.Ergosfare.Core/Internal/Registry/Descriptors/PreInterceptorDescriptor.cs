
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;

namespace Stella.Ergosfare.Core.Internal.Registry.Descriptors;


/// <summary>
/// Represents a descriptor for a pre-interceptor handler.
/// </summary>
/// <remarks>
/// Inherits from <see cref="HandlerDescriptor"/> and implements 
/// <see cref="IPreInterceptorDescriptor"/>.
/// This descriptor contains metadata about the pre-interceptor, 
/// including its handler type, associated message type, groups, 
/// and weight.
/// </remarks>
internal sealed class PreInterceptorDescriptor: HandlerDescriptor, IPreInterceptorDescriptor;