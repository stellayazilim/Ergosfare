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

/// <summary>Command dispatched under <c>ForceMemoizedHandlers</c>.</summary>
public sealed class Memoized : ICommand;

/// <inheritdoc />
public sealed class MemoizedHandler : ICommandHandler<Memoized>
{
    /// <inheritdoc />
    public ValueTask HandleAsync(Memoized command, ErgosfareContext context) => ValueTask.CompletedTask;
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
    public async Task ForceMemoizedHandlers_fails_the_dispatch()
    {
        await using var provider = new ServiceCollection()
            .AddErgosfare(options => options
                .ForceMemoizedHandlers()
                .AddCommandModule(commands => commands.Register<MemoizedHandler>()))
            .BuildServiceProvider();

        var mediator = provider.GetRequiredService<ICommandMediator>();

        // Memoized pipelines are unplanned by contract: a compiled plan resolves or
        // constructs its participants fresh, and a pipeline that caches instances is not
        // the pipeline the plan was compiled against. The two contracts cannot both hold,
        // so every dispatch under ForceMemoizedHandlers fails, saying why.
        var thrown = await Assert.ThrowsAsync<UnplannedDispatchException>(
            async () => await mediator.SendAsync(new Memoized()));

        Assert.Equal(UnplannedDispatchReason.MemoizedInstances, thrown.Reason);
        Assert.Equal(typeof(Memoized), thrown.MessageType);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_users_own_registration_of_a_handler_type_wins_over_the_modules()
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

        Assert.Equal("user-instance", first.Tag);
        Assert.Equal("user-instance", second.Tag);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_users_singleton_registration_serves_every_dispatch_with_the_one_instance()
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

        // An all-singleton pipeline is memoized underneath, but only demanded memoization
        // (ForceMemoizedHandlers) bars a compiled plan: the plan's resolving variant
        // returns the one singleton per dispatch, which is exactly what memoization
        // promises, so the two cannot be told apart and the dispatch runs.
        await mediator.SendAsync(first);
        await mediator.SendAsync(second);

        Assert.Equal("user-instance", first.Tag);
        Assert.Equal("user-instance", second.Tag);
    }
}
