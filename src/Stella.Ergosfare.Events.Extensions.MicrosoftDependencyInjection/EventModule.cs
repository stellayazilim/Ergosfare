using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Events.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection;

/// <summary>
/// The module that registers an application's events and the mediator that publishes them.
/// </summary>
/// <param name="builder">Selects which event constructs this container runs.</param>
internal class EventModule(Action<EventModuleBuilder> builder) : IModule
{
    /// <summary>
    /// Runs the application's selection and registers the event mediator under all three of
    /// its names.
    /// </summary>
    /// <param name="configuration">The container being built and its composition selection.</param>
    public void Build(IModuleConfiguration configuration)
    {
        builder(new EventModuleBuilder(configuration.Compositions));

        // Transient: the mediator holds nothing but the provider that resolved it, and a
        // transient still receives the calling scope's provider.
        configuration.Services.TryAddTransient<IEventMediator, EngineBackedEventMediator>();

        // The same facade under its concrete name, so an application can inject either and
        // a publish through the class is a direct call rather than a virtual one.
        configuration.Services.TryAddTransient<EventMediator>(
            static provider => provider.GetRequiredService<IEventMediator>() as EventMediator
                ?? throw new InvalidOperationException(
                    "The registered IEventMediator is not a EventMediator; a replacement registration cannot serve the concrete facade."));

        // And under the publisher name, for code that reads better asking a publisher to
        // publish.
        configuration.Services.TryAddTransient<IPublisher, EngineBackedEventMediator>();
    }
}
