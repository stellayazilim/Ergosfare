using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Abstractions.Registry;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

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
        public ValueTask HandleAsync(StagedCommand command, IExecutionContext context)
        {
            command.Order.Add("handler");
            return ValueTask.CompletedTask;
        }
    }

    [ExcludeFromDiscovery]
    public sealed class StagedCommandPreInterceptor : ICommandPreInterceptor<StagedCommand>
    {
        public ValueTask<StagedCommand> HandleAsync(StagedCommand command, IExecutionContext context)
        {
            command.Order.Add("pre");
            return ValueTask.FromResult(command);
        }
    }

    [ExcludeFromDiscovery]
    public sealed class StagedCommandPostInterceptor : ICommandPostInterceptor<StagedCommand>
    {
        public ValueTask<object> HandleAsync(StagedCommand command, object messageResult, IExecutionContext context)
        {
            command.Order.Add("post");
            return ValueTask.FromResult(messageResult);
        }
    }

    private sealed class StagedCommandPlan : StagedVoidPlan<StagedCommand>
    {
        public override StagedPlanComposition Composition { get; } = new(
            typeof(StagedCommandHandler),
            [typeof(StagedCommandPreInterceptor)],
            [typeof(StagedCommandPostInterceptor)],
            [],
            []);

        public override async ValueTask Execute(StagedCommand message, IExecutionContext context, IServiceProvider serviceProvider)
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
        public ValueTask HandleAsync(FallbackCommand command, IExecutionContext context)
        {
            command.Order.Add("handler");
            return ValueTask.CompletedTask;
        }
    }

    [ExcludeFromDiscovery]
    public sealed class FallbackCommandPreInterceptor : ICommandPreInterceptor<FallbackCommand>
    {
        public ValueTask<FallbackCommand> HandleAsync(FallbackCommand command, IExecutionContext context)
        {
            command.Order.Add("pre");
            return ValueTask.FromResult(command);
        }
    }

    [ExcludeFromDiscovery]
    public sealed class ExtraFallbackCommandPreInterceptor : ICommandPreInterceptor<FallbackCommand>
    {
        public ValueTask<FallbackCommand> HandleAsync(FallbackCommand command, IExecutionContext context)
        {
            command.Order.Add("extra-pre");
            return ValueTask.FromResult(command);
        }
    }

    private sealed class FallbackCommandPlan : StagedVoidPlan<FallbackCommand>
    {
        public override StagedPlanComposition Composition { get; } = new(
            typeof(FallbackCommandHandler),
            [typeof(FallbackCommandPreInterceptor)],
            [],
            [],
            []);

        public override async ValueTask Execute(FallbackCommand message, IExecutionContext context, IServiceProvider serviceProvider)
        {
            message.Order.Add("staged");
            message = await serviceProvider.GetRequiredService<FallbackCommandPreInterceptor>().HandleAsync(message, context);
            await serviceProvider.GetRequiredService<FallbackCommandHandler>().HandleAsync(message, context);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task RuntimeRegistration_FallsBackToTheStrategy()
    {
        GeneratedDispatchRoots.AddStagedPlan(new FallbackCommandPlan());

        // The extra interceptor is resolvable from the container up front (a runtime
        // registry registration cannot add DI registrations to an already-built
        // provider) but joins the message's pipeline only through the registry below.
        var provider = new ServiceCollection()
            .AddTransient<ExtraFallbackCommandPreInterceptor>()
            .AddErgosfare(x => x.AddCommandModule(c =>
            {
                c.Register<FallbackCommandHandler>();
                c.Register<FallbackCommandPreInterceptor>();
            }))
            .BuildServiceProvider();
        await using var _ = provider;

        var mediator = provider.GetRequiredService<ICommandMediator>();

        var beforeRegistration = new FallbackCommand();
        await mediator.SendAsync(beforeRegistration);
        Assert.Equal(["staged", "pre", "handler"], beforeRegistration.Order);

        // A runtime registration bumps the registry version; the next rebuild sees a
        // second pre-interceptor the plan was not baked for — the advisory contract
        // demands the strategy path, which runs both interceptors.
        provider.GetRequiredService<IMessageRegistry>().Register(typeof(ExtraFallbackCommandPreInterceptor));

        var afterRegistration = new FallbackCommand();
        await mediator.SendAsync(afterRegistration);

        Assert.DoesNotContain("staged", afterRegistration.Order);
        Assert.Contains("pre", afterRegistration.Order);
        Assert.Contains("extra-pre", afterRegistration.Order);
        Assert.Equal("handler", afterRegistration.Order[^1]);
    }

    [ExcludeFromDiscovery]
    public sealed class MismatchedCommand : ICommand
    {
        public List<string> Order { get; } = [];
    }

    [ExcludeFromDiscovery]
    public sealed class MismatchedCommandHandler : ICommandHandler<MismatchedCommand>
    {
        public ValueTask HandleAsync(MismatchedCommand command, IExecutionContext context)
        {
            command.Order.Add("handler");
            return ValueTask.CompletedTask;
        }
    }

    [ExcludeFromDiscovery]
    public sealed class MismatchedCommandPreInterceptor : ICommandPreInterceptor<MismatchedCommand>
    {
        public ValueTask<MismatchedCommand> HandleAsync(MismatchedCommand command, IExecutionContext context)
        {
            command.Order.Add("pre");
            return ValueTask.FromResult(command);
        }
    }

    private sealed class MismatchedCommandPlan : StagedVoidPlan<MismatchedCommand>
    {
        // Baked against a post-interceptor stage the registry never sees — the gate must
        // fail on the very first rebuild and keep the dispatch on the strategy path.
        public override StagedPlanComposition Composition { get; } = new(
            typeof(MismatchedCommandHandler),
            [typeof(MismatchedCommandPreInterceptor)],
            [typeof(StagedCommandPostInterceptor)],
            [],
            []);

        public override ValueTask Execute(MismatchedCommand message, IExecutionContext context, IServiceProvider serviceProvider)
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
        public ValueTask HandleAsync(MemoizedStagedCommand command, IExecutionContext context)
        {
            command.Order.Add("handler");
            return ValueTask.CompletedTask;
        }
    }

    [ExcludeFromDiscovery]
    public sealed class MemoizedStagedCommandPreInterceptor : ICommandPreInterceptor<MemoizedStagedCommand>
    {
        public ValueTask<MemoizedStagedCommand> HandleAsync(MemoizedStagedCommand command, IExecutionContext context)
        {
            command.Order.Add("pre");
            return ValueTask.FromResult(command);
        }
    }

    private sealed class MemoizedStagedCommandPlan : StagedVoidPlan<MemoizedStagedCommand>
    {
        public override StagedPlanComposition Composition { get; } = new(
            typeof(MemoizedStagedCommandHandler),
            [typeof(MemoizedStagedCommandPreInterceptor)],
            [],
            [],
            []);

        public override ValueTask Execute(MemoizedStagedCommand message, IExecutionContext context, IServiceProvider serviceProvider)
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
    public sealed class StagedResultCommand : ICommand<int>
    {
        public List<string> Order { get; } = [];
    }

    [ExcludeFromDiscovery]
    public sealed class StagedResultCommandHandler : ICommandHandler<StagedResultCommand, int>
    {
        public ValueTask<int> HandleAsync(StagedResultCommand command, IExecutionContext context)
        {
            command.Order.Add("handler");
            return ValueTask.FromResult(42);
        }
    }

    [ExcludeFromDiscovery]
    public sealed class StagedResultCommandPreInterceptor : ICommandPreInterceptor<StagedResultCommand>
    {
        public ValueTask<StagedResultCommand> HandleAsync(StagedResultCommand command, IExecutionContext context)
        {
            command.Order.Add("pre");
            return ValueTask.FromResult(command);
        }
    }

    private sealed class StagedResultCommandPlan : StagedResultPlan<StagedResultCommand, int>
    {
        public override StagedPlanComposition Composition { get; } = new(
            typeof(StagedResultCommandHandler),
            [typeof(StagedResultCommandPreInterceptor)],
            [],
            [],
            []);

        public override async ValueTask<int> Execute(StagedResultCommand message, IExecutionContext context, IServiceProvider serviceProvider)
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
}
