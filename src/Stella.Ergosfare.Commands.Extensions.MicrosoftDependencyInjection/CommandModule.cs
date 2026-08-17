using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;

/// <summary>
/// The module that registers an application's commands and the mediator that sends them.
/// </summary>
internal class CommandModule : IModule
{
    private readonly Action<CommandModuleBuilder> _builder;

    /// <summary>
    /// Initializes the module with the registration the application supplied.
    /// </summary>
    /// <param name="builder">Selects which command constructs this container runs.</param>
    public CommandModule(Action<CommandModuleBuilder> builder)
    {
        _builder = builder;
    }

    /// <summary>
    /// Runs the application's selection and registers the command mediator.
    /// </summary>
    /// <param name="configuration">The container being built and its composition selection.</param>
    public void Build(IModuleConfiguration configuration)
    {
        _builder(new CommandModuleBuilder(configuration.Compositions));

        // Transient: the mediator holds nothing but the provider that resolved it, and a
        // transient still receives the calling scope's provider — so participants resolve
        // against the right scope. Registering it scoped would pay the locking and
        // bookkeeping of a scoped resolution on every fresh scope, with nothing to gain.
        configuration.Services.TryAddTransient<ICommandMediator, EngineBackedCommandMediator>();

        // The same facade under its concrete name, so an application can inject either. A
        // send through the interface pays a generic virtual call the JIT cannot resolve
        // ahead of time; through the class it is a direct call. The difference is a few
        // nanoseconds — nothing to most callers, something to a hot loop — so the choice is
        // the caller's, and both names resolve to one object.
        configuration.Services.TryAddTransient<CommandMediator>(
            static provider => Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ICommandMediator>(provider) as CommandMediator
                ?? throw new InvalidOperationException(
                    "The registered ICommandMediator is not a CommandMediator; a replacement registration cannot serve the concrete facade."));
    }
}
