using Ergosfare.Commands.Abstractions;
using Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;

internal class CommandModule : IModule
{
    private readonly Action<CommandModuleBuilder> _builder;

    public CommandModule(Action<CommandModuleBuilder> builder)
    {
        _builder = builder;
    }

    public void Build(IModuleConfiguration configuration)
    {
        _builder(new CommandModuleBuilder(configuration.MessageRegistry));

        configuration.Services.TryAddTransient<ICommandMediator, CommandMediator>();
    }
}