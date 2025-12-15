using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;
using Stella.Ergosfare.Core.Internal.Abstractions;
using Stella.Ergosfare.Core.Internal.Builders;

namespace Stella.Ergosfare.Core.Internal.Factories;

/// <summary>
/// Factory responsible for building <see cref="IHandlerDescriptor"/> instances
/// for a given message or handler type.
/// </summary>
/// <remarks>
/// The factory internally maintains a list of <see cref="IHandlerDescriptorBuilder"/>
/// implementations, including handlers, pre/post interceptors, exception interceptors, 
/// and final interceptors. It can build all applicable descriptors for a given type
/// and supports both synchronous and asynchronous disposal.
/// </remarks>
public class HandlerDescriptorBuilderFactory : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Internal list of descriptor builders used by this factory.
    /// </summary>
    private readonly List<IHandlerDescriptorBuilder> _descriptorBuilders =
    [
        new HandlerDescriptorBuilder(),
        new PreInterceptorDescriptionBuilder(),
        new PostHandlerDescriptorBuilder(),
        new ExceptionInterceptorDescriptorBuilder(),
        new FinalInterceptorDescriptorBuilder()
    ];

    /// <summary>
    /// Builds all applicable <see cref="IHandlerDescriptor"/> instances for the specified message type.
    /// </summary>
    /// <param name="messageType">The message type to build descriptors for.</param>
    /// <returns>
    /// A list of <see cref="IHandlerDescriptor"/> objects that can handle the specified type.
    /// </returns>
    /// <remarks>
    /// Each internal <see cref="IHandlerDescriptorBuilder"/> is checked to see if it
    /// can build descriptors for the given type using <see cref="IHandlerDescriptorBuilder.CanBuild"/>.
    /// </remarks>
    public List<IHandlerDescriptor> BuildDescriptors(Type messageType)
    {
        return _descriptorBuilders
            .Where(d => d.CanBuild(messageType))
            .SelectMany(d => d.Build(messageType))
            .ToList();
    }

    /// <summary>
    /// Performs synchronous cleanup of resources used by this factory.
    /// </summary>
    public void Dispose()
    {
    }

    /// <summary>
    /// Performs asynchronous cleanup of resources used by this factory.
    /// </summary>
    /// <returns>A completed <see cref="ValueTask"/>.</returns>
    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
