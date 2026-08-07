using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Abstractions.Registry;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Command.Test;

/// <summary>
/// The compile-time descriptor catalog behind runtime registration: a manual
/// <c>Register&lt;THandler&gt;()</c> for a catalogued type must serve the precomputed
/// descriptor instance (reference-equal — proof the reflective builders never ran),
/// dispatch through it normally, and stay idempotent against the generated
/// <c>RegisterDescriptors</c> path. Helper types are excluded from discovery so assembly
/// scans cannot register them first.
/// </summary>
public class GeneratedDescriptorCatalogTests
{
    [ExcludeFromDiscovery]
    public sealed class CatalogProbeCommand : ICommand { }

    [ExcludeFromDiscovery]
    public sealed class CatalogProbeCommandHandler : ICommandHandler<CatalogProbeCommand>
    {
        public ValueTask HandleAsync(CatalogProbeCommand command, IExecutionContext context)
        {
            context.Set("catalogRan", true);
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task CatalogedType_RegistersWithThePrecomputedDescriptor_AndDispatches()
    {
        // Exactly what a generated module initializer contributes.
        var descriptor = HandlerDescriptors.Handler(
            typeof(CatalogProbeCommand), typeof(ValueTask), typeof(CatalogProbeCommandHandler));
        GeneratedDescriptorCatalog.Add(
            typeof(CatalogProbeCommandHandler), () => new[] { (IHandlerDescriptor)descriptor });

        await using var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c => c.Register<CatalogProbeCommandHandler>()))
            .BuildServiceProvider();

        // The registry must carry the catalog's very instance — reference equality is the
        // proof the reflective descriptor builders never ran for this type.
        var registry = provider.GetRequiredService<IMessageRegistry>();
        var message = registry.Single(m => m.MessageType == typeof(CatalogProbeCommand));
        Assert.Contains(message.Handlers, h => ReferenceEquals(h, descriptor));

        // And the pipeline built from it dispatches normally.
        var mediator = provider.GetRequiredService<ICommandMediator>();
        var settings = new CommandMediationSettings();
        await mediator.SendAsync(new CatalogProbeCommand(), settings);
        Assert.Equal(true, settings.Items["catalogRan"]);
    }

    [ExcludeFromDiscovery]
    public sealed class DualPathCommand : ICommand { }

    [ExcludeFromDiscovery]
    public sealed class DualPathCommandHandler : ICommandHandler<DualPathCommand>
    {
        public ValueTask HandleAsync(DualPathCommand command, IExecutionContext context)
            => ValueTask.CompletedTask;
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task CatalogRegistration_StaysIdempotent_AgainstRegisterDescriptors()
    {
        var descriptor = HandlerDescriptors.Handler(
            typeof(DualPathCommand), typeof(ValueTask), typeof(DualPathCommandHandler));
        GeneratedDescriptorCatalog.Add(
            typeof(DualPathCommandHandler), () => new[] { (IHandlerDescriptor)descriptor });

        await using var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c => c.Register<DualPathCommandHandler>()))
            .BuildServiceProvider();

        var registry = provider.GetRequiredService<IMessageRegistry>();

        // The generated-registration path following a catalog-backed manual registration
        // must be a no-op: both paths mark the handler type processed.
        registry.RegisterDescriptors([
            HandlerDescriptors.Handler(typeof(DualPathCommand), typeof(ValueTask), typeof(DualPathCommandHandler))
        ]);

        var message = registry.Single(m => m.MessageType == typeof(DualPathCommand));
        Assert.Single(message.Handlers);
    }
}
