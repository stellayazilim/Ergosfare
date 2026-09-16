using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

namespace Stella.Ergosfare.Plugins.Outbox;

public static class OutboxModuleExtensions
{
    public static IModuleRegistry AddOutboxPlugin(this IModuleRegistry registry, Action<OutboxOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(configure);
        var options = new OutboxOptions();
        configure(options);
        return registry.Register(new OutboxModule(options.Snapshot()));
    }

    private sealed class OutboxModule(OutboxOptions options) : IModule
    {
        public void Build(IModuleConfiguration configuration)
        {
            var services = configuration.Services;
            if (services.Any(s => s.ServiceType == typeof(OutboxOptions)))
                throw new InvalidOperationException("AddOutboxPlugin may only be configured once.");
            services.AddSingleton(options);
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<OutboxContextBinding>();
            services.AddLogging();
            options.RegisterStore!(services);
            services.AddHostedService<OutboxBackgroundService>();
        }
    }
}
