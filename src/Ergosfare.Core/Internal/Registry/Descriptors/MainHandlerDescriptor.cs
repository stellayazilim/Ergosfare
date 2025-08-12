using Ergosfare.Core.Abstractions.Registry.Descriptors;

namespace Ergosfare.Core.Internal.Registry.Descriptors;

internal sealed class MainHandlerDescriptor : HandlerDescriptor, IMainHandlerDescriptor
{
    public required Type ResultType { get; init; }
}