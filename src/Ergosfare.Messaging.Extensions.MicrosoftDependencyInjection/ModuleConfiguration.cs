using Ergosfare.Messaging.Abstractions.Registry;
using Microsoft.Extensions.DependencyInjection;

namespace Ergosfare.Messaging.Extensions.MicrosoftDependencyInjection;

internal class ModuleConfiguration(IServiceCollection services, IMessageRegistry messageRegistry)
    : IModuleConfiguration
{
    public IServiceCollection Services { get; } = services;

    public IMessageRegistry MessageRegistry { get; } = messageRegistry;
}