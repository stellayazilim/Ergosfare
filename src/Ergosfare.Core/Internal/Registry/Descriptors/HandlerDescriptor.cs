using Ergosfare.Core.Abstractions.Registry.Descriptors;

namespace Ergosfare.Core.Internal.Registry.Descriptors;

internal abstract class HandlerDescriptor: IHandlerDescriptor
{
    public uint Weight { get; init; }
    
    public required IReadOnlyCollection<string> Groups { get; init; }
    
    public required Type MessageType { get; init;  }

    public required Type HandlerType { get; init; }
}