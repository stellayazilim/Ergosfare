using Ergosfare.Core.Abstractions.Registry.Descriptors;
using Ergosfare.Core.Internal.Abstractions;
using Ergosfare.Core.Internal.Builders;

namespace Ergosfare.Core.Internal.Factories;

public class HandlerDescriptorBuilderFactory: IDisposable, IAsyncDisposable
{
    
    private readonly List<IHandlerDescriptorBuilder> _descriptorBuilders =
    [
        new HandlerDescriptorBuilder(),
        new PreInterceptorDescriptionBuilder(),
        new PostHandlerDescriptorBuilder(),
        new ExceptionInterceptorDescriptorBuilder(),
        new FinalInterceptorDescriptorBuilder()
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
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}