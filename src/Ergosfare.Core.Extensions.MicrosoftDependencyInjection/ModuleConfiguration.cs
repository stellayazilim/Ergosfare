using Ergosfare.Core.Abstractions.Registry;
using Microsoft.Extensions.DependencyInjection;

namespace Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

/// <summary>
/// Represents the configuration context for a module, providing access to the service collection and message registry.
/// </summary>
/// <param name="services">The service collection used for dependency injection.</param>
/// <param name="messageRegistry">The message registry used for registering and resolving message types.</param>
internal class ModuleConfiguration(IServiceCollection services, IMessageRegistry messageRegistry)
    : IModuleConfiguration
{
    /// <summary>
    /// Gets the service collection for registering services.
    /// </summary>
    public IServiceCollection Services { get; } = services;

    /// <summary>
    /// Gets the message registry for registering and resolving message types.
    /// </summary>
    public IMessageRegistry MessageRegistry { get; } = messageRegistry;
}
