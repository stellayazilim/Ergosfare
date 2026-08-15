using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;

/// <summary>
/// Represents a module that registers command handlers and the command mediator.
/// </summary>
internal class CommandModule : IModule
{
    private readonly Action<CommandModuleBuilder> _builder;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandModule"/> class.
    /// </summary>
    /// <param name="builder">An action that configures the <see cref="CommandModuleBuilder"/>.</param>
    public CommandModule(Action<CommandModuleBuilder> builder)
    {
        _builder = builder;
    }

    /// <summary>
    /// Builds the module by applying the provided configuration and registering the <see cref="ICommandMediator"/>.
    /// </summary>
    /// <param name="configuration">The module configuration containing services and this container's frozen composition selection.</param>
    public void Build(IModuleConfiguration configuration)
    {
        _builder(new CommandModuleBuilder(configuration.Compositions));

        // Transient: the mediator is a stateless facade whose only per-instance state is the
        // provider that resolved it — a transient still receives the calling scope's provider,
        // so per-dispatch handler resolution binds to the right scope. Scoped registration
        // would pay the scope lock + resolved-services dictionary insert on every fresh
        // scope (the scope-per-dispatch hot path) with nothing to amortize it. The
        // engine-backed shape makes the facade the only object built per resolution.
        configuration.Services.TryAddTransient<ICommandMediator, EngineBackedCommandMediator>();

        // The same facade under its concrete name, so an application can inject either. A
        // send through the interface pays a generic-virtual dispatch the JIT cannot
        // devirtualize; through the class it is a direct call. Measured at ~5 ns, which is
        // nothing for most callers and everything for a hot loop — so the choice belongs to
        // the caller, and both spellings resolve the one object graph.
        configuration.Services.TryAddTransient<CommandMediator>(
            static provider => global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<ICommandMediator>(provider) as CommandMediator
                ?? throw new global::System.InvalidOperationException(
                    "The registered ICommandMediator is not a CommandMediator; a replacement registration cannot serve the concrete facade."));
    }
}
