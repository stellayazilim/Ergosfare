using Ergosfare.Core.Abstractions.Registry.Descriptors;

namespace Ergosfare.Core.Internal.Registry.Descriptors;

internal sealed class ExceptionInterceptorDescriptor: HandlerDescriptor, IExceptionInterceptorDescriptor
{
    public required Type ResultType { get; init; }
}