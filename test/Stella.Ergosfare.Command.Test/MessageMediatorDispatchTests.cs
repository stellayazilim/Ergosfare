using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Factories;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Internal.Mediator;

namespace Stella.Ergosfare.Command.Test;

public sealed class RelayedCommand : ICommand;

public sealed class RelayedEcho : ICommand<string>;

public sealed class RelayedCommandHandler : ICommandHandler<RelayedCommand>
{
    public ValueTask HandleAsync(RelayedCommand command, ErgosfareContext context)
    {
        context.Set("relayed", true);
        return ValueTask.CompletedTask;
    }
}

public sealed class RelayedEchoHandler : ICommandHandler<RelayedEcho, string>
{
    public ValueTask<string> HandleAsync(RelayedEcho command, ErgosfareContext context)
        => ValueTask.FromResult("relayed");
}

/// <summary>
/// The untyped mediator behind the module facades: it holds the scope's provider, validates
/// what it was given, and hands everything else to the engine. Every module's facade is a
/// thin typed layer over this, so what it does with a null argument — and what it does when
/// it has no engine to delegate to — is the behaviour those facades inherit.
/// </summary>
/// <remarks>
/// The engine is optional at construction for a directly built mediator, which is what makes
/// the "no engine" failure reachable at all: through <c>AddErgosfare</c> the container always
/// supplies one. The message it fails with therefore has to name the registration call, since
/// a caller who hits it built the mediator by hand and has nothing else to go on.
/// </remarks>
public class MessageMediatorDispatchTests
{

    private static ServiceProvider Build()
        => new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c =>
            {
                c.Register<RelayedCommandHandler>();
                c.Register<RelayedEchoHandler>();
            }))
            .BuildServiceProvider();

    private static MessageMediator Wire(IServiceProvider provider)
        => new(
            provider.GetRequiredService<IMessageDependenciesFactory>(),
            provider,
            engine: provider.GetRequiredService<MessageDispatchEngine>());

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task AmbientDispatch_RunsThePipeline_ForBothShapes()
    {
        await using var provider = Build();
        var mediator = Wire(provider);

        await mediator.DispatchAsync(new RelayedCommand());

        Assert.Equal("relayed", await mediator.DispatchAsync<string>(new RelayedEcho()));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ContextDispatch_RunsAgainstTheCallersContext_ForBothShapes()
    {
        await using var provider = Build();
        var mediator = Wire(provider);
        var context = new ErgosfareContext();

        await mediator.DispatchAsync(new RelayedCommand(), context);

        // The caller owns it, so what the handler wrote is still there to read.
        Assert.Equal(true, context.Items["relayed"]);

        var resultContext = new ErgosfareContext();

        Assert.Equal("relayed", await mediator.DispatchAsync<string>(new RelayedEcho(), resultContext));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task EveryOverload_RejectsANullMessage()
    {
        await using var provider = Build();
        var mediator = Wire(provider);
        var context = new ErgosfareContext();

        Assert.Throws<ArgumentNullException>(() => mediator.DispatchAsync(null!));
        Assert.Throws<ArgumentNullException>(() => mediator.DispatchAsync<string>(null!));
        Assert.Throws<ArgumentNullException>(() => mediator.DispatchAsync(null!, context));
        Assert.Throws<ArgumentNullException>(() => mediator.DispatchAsync<string>(null!, context));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ContextOverloads_RejectANullContext()
    {
        await using var provider = Build();
        var mediator = Wire(provider);

        // Checked after the message, so a call missing both is reported against the message
        // first — the argument the caller is more likely to have got wrong.
        var thrown = Assert.Throws<ArgumentNullException>(
            () => mediator.DispatchAsync(new RelayedCommand(), (ErgosfareContext) null!));

        Assert.Equal("context", thrown.ParamName);
        Assert.Throws<ArgumentNullException>(
            () => mediator.DispatchAsync<string>(new RelayedEcho(), (ErgosfareContext) null!));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void Constructor_KeepsTheScopeItWasBuiltFor()
    {
        using var provider = Build();
        var factory = provider.GetRequiredService<IMessageDependenciesFactory>();
        var mediator = new MessageMediator(factory, provider,
            engine: provider.GetRequiredService<MessageDispatchEngine>());

        // The broadcast fast lane reads both of these directly rather than dispatching
        // through the mediator, so they have to be the ones it was constructed with.
        Assert.Same(provider, mediator.ScopeProvider);
        Assert.Same(factory, mediator.DependenciesFactory);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task WithoutAnEngineOrCache_EveryDispatchSaysHowToGetOne()
    {
        await using var provider = Build();

        // Neither an executor cache nor an engine: the shape a hand-built mediator has.
        var mediator = new MessageMediator(
            provider.GetRequiredService<IMessageDependenciesFactory>(), provider);
        var context = new ErgosfareContext();

        var thrown = Assert.Throws<InvalidOperationException>(
            () => mediator.DispatchAsync(new RelayedCommand()));

        Assert.Contains("AddErgosfare", thrown.Message);

        Assert.Throws<InvalidOperationException>(() => mediator.DispatchAsync<string>(new RelayedEcho()));
        Assert.Throws<InvalidOperationException>(() => mediator.DispatchAsync(new RelayedCommand(), context));
        Assert.Throws<InvalidOperationException>(() => mediator.DispatchAsync<string>(new RelayedEcho(), context));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task WithOnlyAnExecutorCache_DispatchesThroughItsOwnEngine()
    {
        await using var provider = Build();

        // The original optional-cache contract: given a cache and no engine, the mediator
        // builds a private one rather than refusing to dispatch.
        var mediator = new MessageMediator(
            provider.GetRequiredService<IMessageDependenciesFactory>(),
            provider,
            provider.GetRequiredService<PipelineExecutorCache>());

        Assert.Equal("relayed", await mediator.DispatchAsync<string>(new RelayedEcho()));
    }
}
