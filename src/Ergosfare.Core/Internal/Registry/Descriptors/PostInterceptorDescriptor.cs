using Ergosfare.Core.Abstractions.Registry.Descriptors;

namespace Ergosfare.Core.Internal.Registry.Descriptors;


/// <summary>
/// Represents a descriptor for a post-interceptor handler.
/// </summary>
/// <remarks>
/// Inherits from <see cref="HandlerDescriptor"/> and implements 
/// <see cref="IPostInterceptorDescriptor"/>.
/// This descriptor contains metadata about the post-interceptor, 
/// including its handler type, associated message type, groups, 
/// weight, and the expected result type.
/// </remarks>
internal sealed class PostInterceptorDescriptor: HandlerDescriptor, IPostInterceptorDescriptor
{
    /// <summary>
    /// Gets or sets the <see cref="Type"/> of the result that this post-interceptor produces.
    /// </summary>
    public required Type ResultType { get; init; }
}