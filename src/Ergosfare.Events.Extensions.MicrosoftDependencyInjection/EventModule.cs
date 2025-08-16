using Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Ergosfare.Events.Abstractions;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ergosfare.Events.Extensions.MicrosoftDependencyInjection;

internal class EventModule : IModule
{
    private readonly Action<EventModuleBuilder> _builder;

    public EventModule(Action<EventModuleBuilder> builder)
    {
        _builder = builder;
    }

    public void Build(IModuleConfiguration configuration)
    {
        _builder(new EventModuleBuilder(configuration.MessageRegistry));

        configuration.Services.TryAddTransient<IEventMediator, EventMediator>();
        configuration.Services.TryAddTransient<IPublisher, EventMediator>();
    }
}