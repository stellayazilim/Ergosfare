using Stella.Ergosfare.Commands;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Command.Test;

public sealed class FacadeScopedProbe
{
    public Guid Id { get; } = Guid.NewGuid();
}

public sealed class ProbeCommand : ICommand { }

public sealed class ProbeCommandHandler(FacadeScopedProbe probe) : ICommandHandler<ProbeCommand>
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

/// <summary>
/// Covers the engine-backed facade shape: DI resolves a single-object facade bound to the
/// process-wide <see cref="MessageDispatchEngine"/>, handler resolution still binds to the
/// calling scope (verified under <c>ValidateScopes</c>), and the facade's two public
/// constructors dispatch identically. Fixtures are top-level and discoverable, so the
/// dispatches run through compiled plans.
/// </summary>
public class EngineBackedFacadeTests
{

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
            .AddScoped<FacadeScopedProbe>()
            .AddErgosfare(x => x.AddCommandModule(c => c.Register<ProbeCommandHandler>()))
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var _ = provider;

        Guid first, second, third;

        using (var scope = provider.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<ICommandMediator>();

            var firstSettings = new ErgosfareContext();
            await mediator.SendAsync(new ProbeCommand(), firstSettings);
            first = Assert.IsType<Guid>(firstSettings.Items["probeId"]);

            var secondSettings = new ErgosfareContext();
            await mediator.SendAsync(new ProbeCommand(), secondSettings);
            second = Assert.IsType<Guid>(secondSettings.Items["probeId"]);
        }

        using (var scope = provider.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<ICommandMediator>();

            var thirdSettings = new ErgosfareContext();
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
    public async Task TypedSend_ThroughTheInterface_RunsTheSamePipeline()
    {
        var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c => c.Register<EchoCommandHandler>()))
            .BuildServiceProvider();
        await using var _ = provider;

        var mediator = provider.GetRequiredService<ICommandMediator>();
        var context = new ErgosfareContext();

        var typed = await mediator.SendAsync<EchoCommand, string>(new EchoCommand { Payload = "hi" }, context);
        var untyped = await mediator.SendAsync(new EchoCommand { Payload = "hi" });

        Assert.Equal("hi!", typed);
        Assert.Equal(untyped, typed);
        Assert.Equal("hi", context.Items["sawPayload"]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void TypedSend_IsImplementedByTheFacade_NotInheritedFromTheDefault()
    {
        // The typed members are default interface methods forwarding to the untyped calls,
        // so an implementation that does not override them still compiles and still returns
        // the right answer — it just never reaches the typed engine path. That failure is
        // invisible: no diagnostic, no wrong result, only the speedup quietly gone. This
        // pins the override so a signature drifting apart from the contract fails here
        // instead of downgrading in silence.
        var declared = typeof(CommandMediator)
            .GetMethods()
            .Where(m => m.Name == nameof(ICommandMediator.SendAsync) && m.GetGenericArguments().Length == 2)
            .ToArray();

        // One per shape: groups, context, cancellation token, GroupSet, string[].
        Assert.Equal(5, declared.Length);
        Assert.All(declared, m => Assert.Equal(typeof(CommandMediator), m.DeclaringType));
    }
}
