using Ergosfare.Core.Abstractions.Registry.Descriptors;

namespace Ergosfare.Core.Internal.Registry.Descriptors;

internal sealed class FinalInterceptorDescriptor: HandlerDescriptor, IFinalInterceptorDescriptor
{
    public required Type ResultType { get; init; }
}