using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Abstractions.DispatchRoots;
using Stella.Ergosfare.Core.Abstractions.StagedPlans;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Core.Abstractions.Results;

namespace Stella.Ergosfare.Command.Test;

/// <summary>
/// Runtime skeleton of the staged pipeline plans: a hand-written
/// <see cref="StagedVoidPlan{TMessage}"/>/<see cref="StagedResultPlan{TMessage,TResult}"/>
/// stands in for what the generator will emit, so these tests validate the hosting
/// executor's advisory gate — the plan runs only while the live pipeline matches its
/// baked composition, and any divergence (a runtime registration, a mismatched
/// composition, memoized instances) routes the dispatch back through the runtime
/// strategy with identical behavior. Participants record execution into the message
/// instance itself, and only the plan writes the "staged" marker, so the chosen path is
/// observable. Helper types are excluded from discovery so assembly scans (the registry
/// is process-wide) cannot alter these pipelines.
/// </summary>
public class StagedPlanExecutionTests
{
    [ExcludeFromDiscovery]
    public sealed class StagedCommand : ICommand
    {
        public List<string> Order { get; } = [];
    }

    [ExcludeFromDiscovery]
    public sealed class StagedCommandHandler : ICommandHandler<StagedCommand>
    {
        public ValueTask HandleAsync(StagedCommand command, ErgosfareContext context)
        {
            command.Order.Add("handler");
            return ValueTask.CompletedTask;
        }
    }

    [ExcludeFromDiscovery]
    public sealed class StagedCommandPreInterceptor : ICommandPreInterceptor<StagedCommand>
    {
        public ValueTask<StagedCommand> HandleAsync(StagedCommand command, ErgosfareContext context)
        {
            command.Order.Add("pre");
            return ValueTask.FromResult(command);
        }
    }

    [ExcludeFromDiscovery]
    public sealed class StagedCommandPostInterceptor : ICommandPostInterceptor<StagedCommand>
    {
        public ValueTask<object> HandleAsync(StagedCommand command, object messageResult, ErgosfareContext context)
        {
            command.Order.Add("post");
            return ValueTask.FromResult(messageResult);
        }
    }

    private sealed class StagedCommandPlan : StagedVoidPlan<StagedCommand>
    {
        public override StagedPlanKey Composition { get; } = new(
            typeof(StagedCommandHandler),
            [typeof(StagedCommandPreInterceptor)],
            [typeof(StagedCommandPostInterceptor)],
            [],
            []);

        public override async ValueTask Execute(StagedCommand message, ErgosfareContext context, IServiceProvider serviceProvider)
        {
            message.Order.Add("staged");
            message = await serviceProvider.GetRequiredService<StagedCommandPreInterceptor>().HandleAsync(message, context);
            await serviceProvider.GetRequiredService<StagedCommandHandler>().HandleAsync(message, context);
            await serviceProvider.GetRequiredService<StagedCommandPostInterceptor>().HandleAsync(message, ValueTask.CompletedTask, context);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task MatchingComposition_ExecutesThroughTheStagedPlan()
    {
        GeneratedDispatchRoots.AddStagedPlan(new StagedCommandPlan());

        var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c =>
            {
                c.Register<StagedCommandHandler>();
                c.Register<StagedCommandPreInterceptor>();
                c.Register<StagedCommandPostInterceptor>();
            }))
            .BuildServiceProvider();
        await using var _ = provider;

        var mediator = provider.GetRequiredService<ICommandMediator>();

        var command = new StagedCommand();
        await mediator.SendAsync(command);

        Assert.Equal(["staged", "pre", "handler", "post"], command.Order);
    }

    [ExcludeFromDiscovery]
    public sealed class FallbackCommand : ICommand
    {
        public List<string> Order { get; } = [];
    }

    [ExcludeFromDiscovery]
    public sealed class FallbackCommandHandler : ICommandHandler<FallbackCommand>
    {
        public ValueTask HandleAsync(FallbackCommand command, ErgosfareContext context)
        {
            command.Order.Add("handler");
            return ValueTask.CompletedTask;
        }
    }

    [ExcludeFromDiscovery]
    public sealed class FallbackCommandPreInterceptor : ICommandPreInterceptor<FallbackCommand>
    {
        public ValueTask<FallbackCommand> HandleAsync(FallbackCommand command, ErgosfareContext context)
        {
            command.Order.Add("pre");
            return ValueTask.FromResult(command);
        }
    }

    [ExcludeFromDiscovery]
    public sealed class ExtraFallbackCommandPreInterceptor : ICommandPreInterceptor<FallbackCommand>
    {
        public ValueTask<FallbackCommand> HandleAsync(FallbackCommand command, ErgosfareContext context)
        {
            command.Order.Add("extra-pre");
            return ValueTask.FromResult(command);
        }
    }

    // Declared to prove the shape compiles against the plan base; never registered, because the point is the fallback that runs without it.
    // ReSharper disable once UnusedType.Local
    private sealed class FallbackCommandPlan : StagedVoidPlan<FallbackCommand>
    {
        public override StagedPlanKey Composition { get; } = new(
            typeof(FallbackCommandHandler),
            [typeof(FallbackCommandPreInterceptor)],
            [],
            [],
            []);

        public override async ValueTask Execute(FallbackCommand message, ErgosfareContext context, IServiceProvider serviceProvider)
        {
            message.Order.Add("staged");
            message = await serviceProvider.GetRequiredService<FallbackCommandPreInterceptor>().HandleAsync(message, context);
            await serviceProvider.GetRequiredService<FallbackCommandHandler>().HandleAsync(message, context);
        }
    }

    [ExcludeFromDiscovery]
    public sealed class MismatchedCommand : ICommand
    {
        public List<string> Order { get; } = [];
    }

    [ExcludeFromDiscovery]
    public sealed class MismatchedCommandHandler : ICommandHandler<MismatchedCommand>
    {
        public ValueTask HandleAsync(MismatchedCommand command, ErgosfareContext context)
        {
            command.Order.Add("handler");
            return ValueTask.CompletedTask;
        }
    }

    [ExcludeFromDiscovery]
    public sealed class MismatchedCommandPreInterceptor : ICommandPreInterceptor<MismatchedCommand>
    {
        public ValueTask<MismatchedCommand> HandleAsync(MismatchedCommand command, ErgosfareContext context)
        {
            command.Order.Add("pre");
            return ValueTask.FromResult(command);
        }
    }

    private sealed class MismatchedCommandPlan : StagedVoidPlan<MismatchedCommand>
    {
        // Baked against a post-interceptor stage the registry never sees — the gate must
        // fail on the very first rebuild and keep the dispatch on the strategy path.
        public override StagedPlanKey Composition { get; } = new(
            typeof(MismatchedCommandHandler),
            [typeof(MismatchedCommandPreInterceptor)],
            [typeof(StagedCommandPostInterceptor)],
            [],
            []);

        public override ValueTask Execute(MismatchedCommand message, ErgosfareContext context, IServiceProvider serviceProvider)
        {
            message.Order.Add("staged");
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task MismatchedComposition_FallsBackToTheStrategy()
    {
        GeneratedDispatchRoots.AddStagedPlan(new MismatchedCommandPlan());

        var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c =>
            {
                c.Register<MismatchedCommandHandler>();
                c.Register<MismatchedCommandPreInterceptor>();
            }))
            .BuildServiceProvider();
        await using var _ = provider;

        var mediator = provider.GetRequiredService<ICommandMediator>();

        var command = new MismatchedCommand();
        await mediator.SendAsync(command);

        Assert.Equal(["pre", "handler"], command.Order);
    }

    [ExcludeFromDiscovery]
    public sealed class MemoizedStagedCommand : ICommand
    {
        public List<string> Order { get; } = [];
    }

    [ExcludeFromDiscovery]
    public sealed class MemoizedStagedCommandHandler : ICommandHandler<MemoizedStagedCommand>
    {
        public ValueTask HandleAsync(MemoizedStagedCommand command, ErgosfareContext context)
        {
            command.Order.Add("handler");
            return ValueTask.CompletedTask;
        }
    }

    [ExcludeFromDiscovery]
    public sealed class MemoizedStagedCommandPreInterceptor : ICommandPreInterceptor<MemoizedStagedCommand>
    {
        public ValueTask<MemoizedStagedCommand> HandleAsync(MemoizedStagedCommand command, ErgosfareContext context)
        {
            command.Order.Add("pre");
            return ValueTask.FromResult(command);
        }
    }

    private sealed class MemoizedStagedCommandPlan : StagedVoidPlan<MemoizedStagedCommand>
    {
        public override StagedPlanKey Composition { get; } = new(
            typeof(MemoizedStagedCommandHandler),
            [typeof(MemoizedStagedCommandPreInterceptor)],
            [],
            [],
            []);

        public override ValueTask Execute(MemoizedStagedCommand message, ErgosfareContext context, IServiceProvider serviceProvider)
        {
            message.Order.Add("staged");
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ForceMemoizedHandlers_FallsBackToTheStrategy()
    {
        GeneratedDispatchRoots.AddStagedPlan(new MemoizedStagedCommandPlan());

        var provider = new ServiceCollection()
            .AddErgosfare(x =>
            {
                x.ForceMemoizedHandlers();
                x.AddCommandModule(c =>
                {
                    c.Register<MemoizedStagedCommandHandler>();
                    c.Register<MemoizedStagedCommandPreInterceptor>();
                });
            })
            .BuildServiceProvider();
        await using var _ = provider;

        var mediator = provider.GetRequiredService<ICommandMediator>();

        // Memoized pipelines cache instances inside their references; a plan resolving
        // from the provider would construct fresh ones — the gate keeps the strategy path.
        var command = new MemoizedStagedCommand();
        await mediator.SendAsync(command);

        Assert.Equal(["pre", "handler"], command.Order);
    }

    [ExcludeFromDiscovery]
    public sealed class DirectStagedCommand : ICommand
    {
        public List<string> Order { get; } = [];
    }

    [ExcludeFromDiscovery]
    public sealed class DirectStagedCommandHandler : ICommandHandler<DirectStagedCommand>
    {
        public ValueTask HandleAsync(DirectStagedCommand command, ErgosfareContext context)
        {
            command.Order.Add("handler");
            return ValueTask.CompletedTask;
        }
    }

    [ExcludeFromDiscovery]
    public sealed class DirectStagedCommandPreInterceptor : ICommandPreInterceptor<DirectStagedCommand>
    {
        public ValueTask<DirectStagedCommand> HandleAsync(DirectStagedCommand command, ErgosfareContext context)
        {
            command.Order.Add("pre");
            return ValueTask.FromResult(command);
        }
    }

    private sealed class DirectStagedCommandPlan : StagedVoidPlan<DirectStagedCommand>
    {
        public override StagedPlanKey Composition { get; } = new(
            typeof(DirectStagedCommandHandler),
            [typeof(DirectStagedCommandPreInterceptor)],
            [],
            [],
            []);

        public override bool SupportsDirectConstruction => true;

        public override async ValueTask Execute(DirectStagedCommand message, ErgosfareContext context, IServiceProvider serviceProvider)
        {
            message.Order.Add("staged");
            message = await serviceProvider.GetRequiredService<DirectStagedCommandPreInterceptor>().HandleAsync(message, context);
            await serviceProvider.GetRequiredService<DirectStagedCommandHandler>().HandleAsync(message, context);
        }

        public override async ValueTask ExecuteDirect(DirectStagedCommand message, ErgosfareContext context, IServiceProvider serviceProvider)
        {
            message.Order.Add("staged-direct");
            message = await new DirectStagedCommandPreInterceptor().HandleAsync(message, context);
            await new DirectStagedCommandHandler().HandleAsync(message, context);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task PlainTransientParticipants_ExecuteThroughTheDirectVariant()
    {
        GeneratedDispatchRoots.AddStagedPlan(new DirectStagedCommandPlan());

        var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c =>
            {
                c.Register<DirectStagedCommandHandler>();
                c.Register<DirectStagedCommandPreInterceptor>();
            }))
            .BuildServiceProvider();
        await using var _ = provider;

        var command = new DirectStagedCommand();
        await provider.GetRequiredService<ICommandMediator>().SendAsync(command);

        Assert.Equal(["staged-direct", "pre", "handler"], command.Order);
    }

    [ExcludeFromDiscovery]
    public sealed class OverriddenDirectCommand : ICommand
    {
        public List<string> Order { get; } = [];
    }

    [ExcludeFromDiscovery]
    public sealed class OverriddenDirectCommandHandler : ICommandHandler<OverriddenDirectCommand>
    {
        public ValueTask HandleAsync(OverriddenDirectCommand command, ErgosfareContext context)
        {
            command.Order.Add("handler");
            return ValueTask.CompletedTask;
        }
    }

    [ExcludeFromDiscovery]
    public sealed class OverriddenDirectCommandPreInterceptor : ICommandPreInterceptor<OverriddenDirectCommand>
    {
        public ValueTask<OverriddenDirectCommand> HandleAsync(OverriddenDirectCommand command, ErgosfareContext context)
        {
            command.Order.Add("pre");
            return ValueTask.FromResult(command);
        }
    }

    private sealed class OverriddenDirectCommandPlan : StagedVoidPlan<OverriddenDirectCommand>
    {
        public override StagedPlanKey Composition { get; } = new(
            typeof(OverriddenDirectCommandHandler),
            [typeof(OverriddenDirectCommandPreInterceptor)],
            [],
            [],
            []);

        public override bool SupportsDirectConstruction => true;

        public override async ValueTask Execute(OverriddenDirectCommand message, ErgosfareContext context, IServiceProvider serviceProvider)
        {
            message.Order.Add("staged");
            message = await serviceProvider.GetRequiredService<OverriddenDirectCommandPreInterceptor>().HandleAsync(message, context);
            await serviceProvider.GetRequiredService<OverriddenDirectCommandHandler>().HandleAsync(message, context);
        }

        public override ValueTask ExecuteDirect(OverriddenDirectCommand message, ErgosfareContext context, IServiceProvider serviceProvider)
        {
            message.Order.Add("staged-direct");
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task LifetimeOverride_KeepsTheProviderResolvingVariant()
    {
        GeneratedDispatchRoots.AddStagedPlan(new OverriddenDirectCommandPlan());

        // The user's singleton registration (before AddErgosfare, so the module's
        // TryAddTransient defers to it) breaks the per-participant plain-transient
        // proof — the composition still matches, so the plan runs, but through its
        // provider-resolving variant, which honors the override.
        var provider = new ServiceCollection()
            .AddSingleton<OverriddenDirectCommandHandler>()
            .AddErgosfare(x => x.AddCommandModule(c =>
            {
                c.Register<OverriddenDirectCommandHandler>();
                c.Register<OverriddenDirectCommandPreInterceptor>();
            }))
            .BuildServiceProvider();
        await using var _ = provider;

        var command = new OverriddenDirectCommand();
        await provider.GetRequiredService<ICommandMediator>().SendAsync(command);

        Assert.Equal(["staged", "pre", "handler"], command.Order);
    }

    [ExcludeFromDiscovery]
    public sealed class StagedResultCommand : ICommand<int>
    {
        public List<string> Order { get; } = [];
    }

    [ExcludeFromDiscovery]
    public sealed class StagedResultCommandHandler : ICommandHandler<StagedResultCommand, int>
    {
        public ValueTask<int> HandleAsync(StagedResultCommand command, ErgosfareContext context)
        {
            command.Order.Add("handler");
            return ValueTask.FromResult(42);
        }
    }

    [ExcludeFromDiscovery]
    public sealed class StagedResultCommandPreInterceptor : ICommandPreInterceptor<StagedResultCommand>
    {
        public ValueTask<StagedResultCommand> HandleAsync(StagedResultCommand command, ErgosfareContext context)
        {
            command.Order.Add("pre");
            return ValueTask.FromResult(command);
        }
    }

    private sealed class StagedResultCommandPlan : StagedResultPlan<StagedResultCommand, int>
    {
        public override StagedPlanKey Composition { get; } = new(
            typeof(StagedResultCommandHandler),
            [typeof(StagedResultCommandPreInterceptor)],
            [],
            [],
            []);

        public override async ValueTask<int> Execute(StagedResultCommand message, ErgosfareContext context, IServiceProvider serviceProvider)
        {
            message.Order.Add("staged");
            message = await serviceProvider.GetRequiredService<StagedResultCommandPreInterceptor>().HandleAsync(message, context);
            return await serviceProvider.GetRequiredService<StagedResultCommandHandler>().HandleAsync(message, context);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task MatchingComposition_ExecutesThroughTheStagedResultPlan()
    {
        GeneratedDispatchRoots.AddStagedPlan(new StagedResultCommandPlan());

        var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c =>
            {
                c.Register<StagedResultCommandHandler>();
                c.Register<StagedResultCommandPreInterceptor>();
            }))
            .BuildServiceProvider();
        await using var _ = provider;

        var mediator = provider.GetRequiredService<ICommandMediator>();

        var command = new StagedResultCommand();
        var result = await mediator.SendAsync(command);

        Assert.Equal(42, result);
        Assert.Equal(["staged", "pre", "handler"], command.Order);
    }

    [ExcludeFromDiscovery]
    public sealed class AdapterGateCommand : ICommand<Result<int>>
    {
        public List<string> Order { get; } = [];
    }

    [ExcludeFromDiscovery]
    public sealed class AdapterGateCommandHandler : ICommandHandler<AdapterGateCommand, Result<int>>
    {
        public ValueTask<Result<int>> HandleAsync(AdapterGateCommand command, ErgosfareContext context)
        {
            command.Order.Add("handler");
            return ValueTask.FromResult(Result<int>.Ok(42));
        }
    }

    [ExcludeFromDiscovery]
    public sealed class AdapterGateCommandPreInterceptor : ICommandPreInterceptor<AdapterGateCommand>
    {
        public ValueTask<AdapterGateCommand> HandleAsync(AdapterGateCommand command, ErgosfareContext context)
        {
            command.Order.Add("pre");
            return ValueTask.FromResult(command);
        }
    }

    private sealed class AdapterGateCommandPlan : StagedResultPlan<AdapterGateCommand, Result<int>>
    {
        // The composition matches the live pipeline exactly — but bakes no adapter type,
        // while the Result<int> slot binds the native adapter at runtime.
        public override StagedPlanKey Composition { get; } = new(
            typeof(AdapterGateCommandHandler),
            [typeof(AdapterGateCommandPreInterceptor)],
            [],
            [],
            []);

        public override async ValueTask<Result<int>> Execute(AdapterGateCommand message, ErgosfareContext context, IServiceProvider serviceProvider)
        {
            message.Order.Add("staged");
            message = await serviceProvider.GetRequiredService<AdapterGateCommandPreInterceptor>().HandleAsync(message, context);
            return await serviceProvider.GetRequiredService<AdapterGateCommandHandler>().HandleAsync(message, context);
        }
    }

    [ExcludeFromDiscovery]
    public sealed class AdapterMatchCommand : ICommand<Result<int>>
    {
        public List<string> Order { get; } = [];
    }

    [ExcludeFromDiscovery]
    public sealed class AdapterMatchCommandHandler : ICommandHandler<AdapterMatchCommand, Result<int>>
    {
        public ValueTask<Result<int>> HandleAsync(AdapterMatchCommand command, ErgosfareContext context)
        {
            command.Order.Add("handler");
            return ValueTask.FromResult(Result<int>.Ok(42));
        }
    }

    [ExcludeFromDiscovery]
    public sealed class AdapterMatchCommandPreInterceptor : ICommandPreInterceptor<AdapterMatchCommand>
    {
        public ValueTask<AdapterMatchCommand> HandleAsync(AdapterMatchCommand command, ErgosfareContext context)
        {
            command.Order.Add("pre");
            return ValueTask.FromResult(command);
        }
    }

    private sealed class AdapterMatchCommandPlan : StagedResultPlan<AdapterMatchCommand, Result<int>>
    {
        // The same shape as the mismatch scenario, now baking the adapter identity the
        // runtime binds for the Result<int> slot — the gate's admission ticket.
        public override StagedPlanKey Composition { get; } = new(
            typeof(AdapterMatchCommandHandler),
            [typeof(AdapterMatchCommandPreInterceptor)],
            [],
            [],
            [],
            typeof(ResultExceptionAdapter<int>));

        public override async ValueTask<Result<int>> Execute(AdapterMatchCommand message, ErgosfareContext context, IServiceProvider serviceProvider)
        {
            message.Order.Add("staged");
            message = await serviceProvider.GetRequiredService<AdapterMatchCommandPreInterceptor>().HandleAsync(message, context);
            return await serviceProvider.GetRequiredService<AdapterMatchCommandHandler>().HandleAsync(message, context);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task AdapterMatch_AdmitsTheStagedResultPlan()
    {
        GeneratedDispatchRoots.AddStagedPlan(new AdapterMatchCommandPlan());

        var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c =>
            {
                c.Register<AdapterMatchCommandHandler>();
                c.Register<AdapterMatchCommandPreInterceptor>();
            }))
            .BuildServiceProvider();
        await using var _ = provider;

        var mediator = provider.GetRequiredService<ICommandMediator>();

        var command = new AdapterMatchCommand();
        var result = await mediator.SendAsync(command);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Equal(["staged", "pre", "handler"], command.Order);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task AdapterMismatch_KeepsTheDispatchOffThePlan()
    {
        GeneratedDispatchRoots.AddStagedPlan(new AdapterGateCommandPlan());

        var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c =>
            {
                c.Register<AdapterGateCommandHandler>();
                c.Register<AdapterGateCommandPreInterceptor>();
            }))
            .BuildServiceProvider();
        await using var _ = provider;

        var mediator = provider.GetRequiredService<ICommandMediator>();

        var command = new AdapterGateCommand();
        var result = await mediator.SendAsync(command);

        // A plan emitted without the slot's value-path branches must never serve an
        // adapted pipeline: the adapter-identity gate routes the dispatch back through
        // the runtime strategy, which owns the probing.
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Equal(["pre", "handler"], command.Order);
    }

    public sealed class DefaultGateOutcome
    {
        public Exception? Error { get; init; }
        public int Value { get; init; }
    }

    [ExcludeFromDiscovery]
    public sealed class DefaultGateCommand : ICommand<DefaultGateOutcome>
    {
        public List<string> Order { get; } = [];
    }

    [ExcludeFromDiscovery]
    public sealed class DefaultGateCommandHandler : ICommandHandler<DefaultGateCommand, DefaultGateOutcome>
    {
        public ValueTask<DefaultGateOutcome> HandleAsync(DefaultGateCommand command, ErgosfareContext context)
        {
            command.Order.Add("handler");
            return ValueTask.FromResult(new DefaultGateOutcome { Value = 42 });
        }
    }

    [ExcludeFromDiscovery]
    public sealed class DefaultGateCommandPreInterceptor : ICommandPreInterceptor<DefaultGateCommand>
    {
        public ValueTask<DefaultGateCommand> HandleAsync(DefaultGateCommand command, ErgosfareContext context)
        {
            command.Order.Add("pre");
            return ValueTask.FromResult(command);
        }
    }

    private sealed class DefaultGateCommandPlan : StagedResultPlan<DefaultGateCommand, DefaultGateOutcome>
    {
        // Matches the live pipeline but models no adapter — while the container's
        // configured default serves the slot, the plan must stand down.
        public override StagedPlanKey Composition { get; } = new(
            typeof(DefaultGateCommandHandler),
            [typeof(DefaultGateCommandPreInterceptor)],
            [],
            [],
            []);

        public override async ValueTask<DefaultGateOutcome> Execute(DefaultGateCommand message, ErgosfareContext context, IServiceProvider serviceProvider)
        {
            message.Order.Add("staged");
            message = await serviceProvider.GetRequiredService<DefaultGateCommandPreInterceptor>().HandleAsync(message, context);
            return await serviceProvider.GetRequiredService<DefaultGateCommandHandler>().HandleAsync(message, context);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ConfiguredDefaultAdapter_KeepsTheDispatchOffAnUnadaptedPlan()
    {
        GeneratedDispatchRoots.AddStagedPlan(new DefaultGateCommandPlan());

        var provider = new ServiceCollection()
            .AddErgosfare(x => x
                .UseDefaultResultAdapter(typeof(CommandTestDefaultResultAdapter))
                .AddCommandModule(c =>
                {
                    c.Register<DefaultGateCommandHandler>();
                    c.Register<DefaultGateCommandPreInterceptor>();
                }))
            .BuildServiceProvider();
        await using var _ = provider;

        var mediator = provider.GetRequiredService<ICommandMediator>();

        var command = new DefaultGateCommand();
        var result = await mediator.SendAsync(command);

        // The generator cannot see the container's default adapter, so its plans carry
        // no branches for it — the adapter-identity gate keeps such dispatches on the
        // runtime strategy, which consults the default.
        Assert.Equal(42, result.Value);
        Assert.Equal(["pre", "handler"], command.Order);
    }
}
