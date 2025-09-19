using Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Ergosfare.Events.Abstractions;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ergosfare.Events.Extensions.MicrosoftDependencyInjection;

/// <summary>
/// Represents the event module for the application, which registers the event mediation
/// pipeline and its associated services.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="EventModule"/> allows registration of event handlers, pre-, post-, final-, 
/// and exception interceptors within the event mediation pipeline.
/// </para>
/// <para>
/// It also registers the core services <see cref="IEventMediator"/> and <see cref="IPublisher"/>
/// in the dependency injection container.
/// </para>
/// </remarks>
internal class EventModule(Action<EventModuleBuilder> builder) : IModule
{
    
    /// <summary>
    /// Configures the event module using the specified module configuration.
    /// </summary>
    /// <param name="configuration">The module configuration containing services and message registry.</param>
    public void Build(IModuleConfiguration configuration)
    {
        builder(new EventModuleBuilder(configuration.MessageRegistry));

        configuration.Services.TryAddTransient<IEventMediator, EventMediator>();
        configuration.Services.TryAddTransient<IPublisher, EventMediator>();
    }
}