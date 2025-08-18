using Ergosfare.Core.Abstractions.Registry.Descriptors;

namespace Ergosfare.Core.Internal.Registry.Descriptors;

internal sealed class PostInterceptorDescriptor: HandlerDescriptor, IPostInterceptorDescriptor
{
    public required Type ResultType { get; init; }
}