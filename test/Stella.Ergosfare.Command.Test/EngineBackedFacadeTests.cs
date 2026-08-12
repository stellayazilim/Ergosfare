using Stella.Ergosfare.Commands;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Command.Test;

/// <summary>
/// Covers the engine-backed facade shape: DI resolves a single-object facade bound to the
/// process-wide <see cref="MessageDispatchEngine"/>, handler resolution still binds to the
/// calling scope (verified under <c>ValidateScopes</c>), and the facade's two public
/// constructors dispatch identically.
/// </summary>
public class EngineBackedFacadeTests
{
    public sealed class ScopedProbe
    {
        public Guid Id { get; } = Guid.NewGuid();
    }

    public sealed class ProbeCommand : ICommand { }

    public sealed class ProbeCommandHandler(ScopedProbe probe) : ICommandHandler<ProbeCommand>
    {
        public ValueTask HandleAsync(ProbeCommand command, ErgosfareContext context)
        {
            context.Set("probeId", probe.Id);
            return ValueTask.CompletedTask;
        }
    }

    public sealed class EchoCommand : ICommand<string>
    {
        public string Payload { get; init; } = string.Empty;
    }

    public sealed class EchoCommandHandler : ICommandHandler<EchoCommand, string>
    {
        public ValueTask<string> HandleAsync(EchoCommand command, ErgosfareContext context)
        {
            context.Set("sawPayload", command.Payload);
            return ValueTask.FromResult(command.Payload + "!");
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task DiResolvedFacade_IsTheEngineBackedShape()
    {
        var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c => c.Register<EchoCommandHandler>()))
            .BuildServiceProvider();
        await using var _ = provider;

        var mediator = provider.GetRequiredService<ICommandMediator>();

        // The DI shape derives from the public facade (compat for callers typed to it) but
        // is not the bare facade — it must be the single-constructor engine-backed type.
        Assert.IsAssignableFrom<CommandMediator>(mediator);
        Assert.NotEqual(typeof(CommandMediator), mediator.GetType());
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task DiResolvedFacade_WithValidateScopes_BindsHandlerResolutionToTheCallingScope()
    {
        var provider = new ServiceCollection()
            .AddScoped<ScopedProbe>()
            .AddErgosfare(x => x.AddCommandModule(c => c.Register<ProbeCommandHandler>()))
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var _ = provider;

        Guid first, second, third;

        using (var scope = provider.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<ICommandMediator>();

            var firstSettings = new CommandMediationSettings();
            await mediator.SendAsync(new ProbeCommand(), firstSettings);
            first = Assert.IsType<Guid>(firstSettings.Items["probeId"]);

            var secondSettings = new CommandMediationSettings();
            await mediator.SendAsync(new ProbeCommand(), secondSettings);
            second = Assert.IsType<Guid>(secondSettings.Items["probeId"]);
        }

        using (var scope = provider.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<ICommandMediator>();

            var thirdSettings = new CommandMediationSettings();
            await mediator.SendAsync(new ProbeCommand(), thirdSettings);
            third = Assert.IsType<Guid>(thirdSettings.Items["probeId"]);
        }

        // Within one scope the scoped dependency is one instance; a fresh scope gets its own.
        Assert.Equal(first, second);
        Assert.NotEqual(first, third);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task BothConstructors_DispatchIdentically()
    {
        var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c => c.Register<EchoCommandHandler>()))
            .BuildServiceProvider();
        await using var _ = provider;

        var engineBacked = new CommandMediator(
            provider.GetRequiredService<MessageDispatchEngine>(), provider);
        var mediatorBacked = new CommandMediator(
            provider.GetRequiredService<IMessageMediator>());

        foreach (var mediator in new[] { engineBacked, mediatorBacked })
        {
            var settings = new CommandMediationSettings();

            var result = await mediator.SendAsync(
                new EchoCommand { Payload = "hi" }, settings);

            Assert.Equal("hi!", result);
            Assert.Equal("hi", settings.Items["sawPayload"]);
        }
    }
}
