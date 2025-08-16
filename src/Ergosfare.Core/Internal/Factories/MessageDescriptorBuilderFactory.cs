using Ergosfare.Core.Abstractions.Registry.Descriptors;
using Ergosfare.Core.Internal.Abstractions;
using Ergosfare.Core.Internal.Builders;

namespace Ergosfare.Core.Internal.Factories;

public class MessageDescriptorBuilderFactory: IDisposable, IAsyncDisposable
{
    
    private readonly List<IHandlerDescriptorBuilder> _descriptorBuilders =
    [
        new HandlerDescriptorBuilder(),
    ];


    public List<IHandlerDescriptor> BuildDescriptors(Type messageType)
    {
        return _descriptorBuilders
            .Where(d => d.CanBuild(messageType))
            .SelectMany(d => d.Build(messageType))
            .ToList();

    }

    public void Dispose()
    {
        // TODO release managed resources here
    }

    public ValueTask DisposeAsync()
    {
        // TODO release managed resources here
        return ValueTask.CompletedTask;
    }
}