using Ergosfare.Commands.Abstractions;
using Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;

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
    /// <param name="configuration">The module configuration containing services and message registry.</param>
    public void Build(IModuleConfiguration configuration)
    {
        _builder(new CommandModuleBuilder(configuration.MessageRegistry));

        configuration.Services.TryAddTransient<ICommandMediator, CommandMediator>();
    }
}