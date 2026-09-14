using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

namespace Stella.Ergosfare.Contract.Test.Lifetime;

// Top-level and unkeyed so the generator bakes each message's plan; every type stays scoped
// to this area.

/// <summary>Command whose participants count their constructions.</summary>
public sealed class Counted : ICommand;

/// <inheritdoc />
public sealed class CountedHandler : ICommandHandler<Counted>
{
    /// <summary>Constructions since the last reset; the scenario resets before use.</summary>
    public static int Constructions;

    /// <summary>Counts this construction.</summary>
    public CountedHandler() => Interlocked.Increment(ref Constructions);

    /// <inheritdoc />
    public ValueTask HandleAsync(Counted command, ErgosfareContext context) => ValueTask.CompletedTask;
}

/// <inheritdoc />
public sealed class CountedPre : ICommandPreInterceptor<Counted>
{
    /// <inheritdoc cref="CountedHandler.Constructions"/>
    public static int Constructions;

    /// <summary>Counts this construction.</summary>
    public CountedPre() => Interlocked.Increment(ref Constructions);

    /// <inheritdoc />
    public ValueTask<Counted> HandleAsync(Counted command, ErgosfareContext context)
        => ValueTask.FromResult(command);
}

/// <summary>Command whose handler the user registers as their own instance.</summary>
public sealed class Owned : ICommand
{
    /// <summary>The tag of the handler instance that ran.</summary>
    public string? Tag;
}

/// <inheritdoc />
public sealed class OwnedHandler : ICommandHandler<Owned>
{
    /// <summary>Distinguishes the user's instance from a container-built one.</summary>
    public string Tag { get; init; } = "module-built";

    /// <inheritdoc />
    public ValueTask HandleAsync(Owned command, ErgosfareContext context)
    {
        command.Tag = Tag;
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// How many times a pipeline participant is constructed, and who decides. Instance counts
/// are contract because lifetime says so — the scenarios below never assert on pooling or
/// caching, only on what a user's own DI registration promises.
/// </summary>
public sealed class HandlerLifetimeTests
{
    private static ServiceProvider CreateProvider()
        => new ServiceCollection()
            .AddErgosfare(options => options
                .AddCommandModule(commands => commands
                    .Register<CountedHandler>()
                    .Register<CountedPre>()))
            .BuildServiceProvider();

    [Fact]
    [Trait("Category", "Contract")]
    public async Task Handlers_are_constructed_once_per_dispatch_by_default()
    {
        CountedHandler.Constructions = 0;

        await using var provider = CreateProvider();

        var mediator = provider.GetRequiredService<ICommandMediator>();
        await mediator.SendAsync(new Counted());
        await mediator.SendAsync(new Counted());
        await mediator.SendAsync(new Counted());

        Assert.Equal(3, CountedHandler.Constructions);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task Interceptors_are_constructed_once_per_dispatch_by_default()
    {
        CountedPre.Constructions = 0;

        await using var provider = CreateProvider();

        var mediator = provider.GetRequiredService<ICommandMediator>();
        await mediator.SendAsync(new Counted());
        await mediator.SendAsync(new Counted());

        Assert.Equal(2, CountedPre.Constructions);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_parameterless_handler_is_constructed_by_the_plan_despite_a_DI_factory()
    {
        // Transient on purpose: a singleton registration makes every participant of the
        // pipeline singleton, which the engine counts as memoization — and memoized
        // pipelines are unplanned by contract (see the scenario below). A transient
        // factory carries the same claim this scenario pins: the user's registration, not
        // the module's, is the one the dispatch resolves.
        await using var provider = new ServiceCollection()
            .AddTransient(_ => new OwnedHandler { Tag = "user-instance" })
            .AddErgosfare(options => options
                .AddCommandModule(commands => commands.Register<OwnedHandler>()))
            .BuildServiceProvider();

        var first = new Owned();
        var second = new Owned();
        var mediator = provider.GetRequiredService<ICommandMediator>();
        await mediator.SendAsync(first);
        await mediator.SendAsync(second);

        Assert.Equal("module-built", first.Tag);
        Assert.Equal("module-built", second.Tag);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_parameterless_handler_is_constructed_by_the_plan_despite_a_DI_singleton()
    {
        var chosen = new OwnedHandler { Tag = "user-instance" };

        await using var provider = new ServiceCollection()
            .AddSingleton(chosen)
            .AddErgosfare(options => options
                .AddCommandModule(commands => commands.Register<OwnedHandler>()))
            .BuildServiceProvider();

        var mediator = provider.GetRequiredService<ICommandMediator>();
        var first = new Owned();
        var second = new Owned();

        // The generated plan constructs a parameterless handler without consulting DI.
        await mediator.SendAsync(first);
        await mediator.SendAsync(second);

        Assert.Equal("module-built", first.Tag);
        Assert.Equal("module-built", second.Tag);
    }
}
