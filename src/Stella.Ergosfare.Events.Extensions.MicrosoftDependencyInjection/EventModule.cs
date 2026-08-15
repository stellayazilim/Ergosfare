using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Events.Abstractions;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection;

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
    /// <param name="configuration">The module configuration containing services and this container's frozen composition selection.</param>
    public void Build(IModuleConfiguration configuration)
    {
        builder(new EventModuleBuilder(configuration.Compositions));

        // Transient, not scoped: the mediator is stateless and a transient service is handed
        // the resolving scope's provider all the same, so per-dispatch handler resolution
        // still binds to the calling scope. Scoped would add a scope lock and a
        // resolved-services dictionary insert to every dispatch for no benefit. The
        // engine-backed shape makes the facade the only object built per resolution.
        configuration.Services.TryAddTransient<IEventMediator, EngineBackedEventMediator>();

        // The same facade under its concrete name, so an application can inject either. A
        // publish through the interface pays a generic-virtual dispatch the JIT cannot
        // devirtualize; through the class it is a direct call. Measured at ~5 ns, which is
        // nothing for most callers and everything for a hot loop — so the choice belongs to
        // the caller, and both spellings resolve the one object graph.
        configuration.Services.TryAddTransient<EventMediator>(
            static provider => global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IEventMediator>(provider) as EventMediator
                ?? throw new global::System.InvalidOperationException(
                    "The registered IEventMediator is not a EventMediator; a replacement registration cannot serve the concrete facade."));
        configuration.Services.TryAddTransient<IPublisher, EngineBackedEventMediator>();
    }
}
