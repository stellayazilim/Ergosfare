using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Generated;

namespace Stella.Ergosfare.Contract.Test.Lifetime;

/// <summary>
/// How many times a pipeline participant is constructed, and who decides. Instance counts
/// are contract because lifetime says so — the scenarios below never assert on pooling or
/// caching, only on what a user's own DI registration promises.
/// </summary>
public sealed class HandlerLifetimeTests
{
    private const string Key = "contract.lifetime";

    // --- default lifetime -----------------------------------------------------

    [DiscoveryKey(Key)]
    public sealed class Counted : ICommand;

    [DiscoveryKey(Key)]
    public sealed class CountedHandler : ICommandHandler<Counted>
    {
        /// <summary>Constructions since the last reset; the scenario resets before use.</summary>
        public static int Constructions;

        public CountedHandler() => Interlocked.Increment(ref Constructions);

        public ValueTask HandleAsync(Counted command, IExecutionContext context) => ValueTask.CompletedTask;
    }

    [DiscoveryKey(Key)]
    public sealed class CountedPre : ICommandPreInterceptor<Counted>
    {
        /// <inheritdoc cref="CountedHandler.Constructions"/>
        public static int Constructions;

        public CountedPre() => Interlocked.Increment(ref Constructions);

        public ValueTask<Counted> HandleAsync(Counted command, IExecutionContext context)
            => ValueTask.FromResult(command);
    }

    // --- forced memoization ---------------------------------------------------

    [DiscoveryKey(Key)]
    public sealed class Memoized : ICommand;

    [DiscoveryKey(Key)]
    public sealed class MemoizedHandler : ICommandHandler<Memoized>
    {
        /// <inheritdoc cref="CountedHandler.Constructions"/>
        public static int Constructions;

        public MemoizedHandler() => Interlocked.Increment(ref Constructions);

        public ValueTask HandleAsync(Memoized command, IExecutionContext context) => ValueTask.CompletedTask;
    }

    // --- user-owned registration ---------------------------------------------

    [DiscoveryKey(Key)]
    public sealed class Owned : ICommand
    {
        /// <summary>The tag of the handler instance that ran.</summary>
        public string? Tag;
    }

    [DiscoveryKey(Key)]
    public sealed class OwnedHandler : ICommandHandler<Owned>
    {
        /// <summary>Distinguishes the user's instance from a container-built one.</summary>
        public string Tag { get; init; } = "module-built";

        public ValueTask HandleAsync(Owned command, IExecutionContext context)
        {
            command.Tag = Tag;
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task Handlers_are_constructed_once_per_dispatch_by_default()
    {
        CountedHandler.Constructions = 0;

        await using var provider = new ServiceCollection()
            .AddErgosfare(options => options.AddCommandModule(commands => commands.RegisterGenerated(Key)))
            .BuildServiceProvider();

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

        await using var provider = new ServiceCollection()
            .AddErgosfare(options => options.AddCommandModule(commands => commands.RegisterGenerated(Key)))
            .BuildServiceProvider();

        var mediator = provider.GetRequiredService<ICommandMediator>();
        await mediator.SendAsync(new Counted());
        await mediator.SendAsync(new Counted());

        Assert.Equal(2, CountedPre.Constructions);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task ForceMemoizedHandlers_reuses_one_instance_across_dispatches()
    {
        MemoizedHandler.Constructions = 0;

        await using var provider = new ServiceCollection()
            .AddErgosfare(options => options
                .ForceMemoizedHandlers()
                .AddCommandModule(commands => commands.RegisterGenerated(Key)))
            .BuildServiceProvider();

        var mediator = provider.GetRequiredService<ICommandMediator>();
        await mediator.SendAsync(new Memoized());
        await mediator.SendAsync(new Memoized());
        await mediator.SendAsync(new Memoized());

        Assert.Equal(1, MemoizedHandler.Constructions);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_users_own_registration_of_a_handler_type_wins_over_the_modules()
    {
        var chosen = new OwnedHandler { Tag = "user-instance" };

        await using var provider = new ServiceCollection()
            .AddSingleton(chosen)
            .AddErgosfare(options => options.AddCommandModule(commands => commands.RegisterGenerated(Key)))
            .BuildServiceProvider();

        var first = new Owned();
        var second = new Owned();
        var mediator = provider.GetRequiredService<ICommandMediator>();
        await mediator.SendAsync(first);
        await mediator.SendAsync(second);

        Assert.Equal("user-instance", first.Tag);
        Assert.Equal("user-instance", second.Tag);
    }
}
