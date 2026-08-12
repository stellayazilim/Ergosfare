using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Abstractions.DispatchRoots;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Command.Test;

/// <summary>
/// Runtime behavior of generated void pipeline plans, installed here through the public
/// <see cref="GeneratedDispatchRoots.AddVoidPlan{TMessage, THandler}()"/> surface exactly as
/// generated code would: the plan-closed executor dispatches the handler, runtime
/// registrations still invalidate the cached pipeline, and a plan whose handler type does
/// not match the actual registration falls back to the runtime dispatch shape without any
/// behavioral difference. Helper types are excluded from discovery so assembly scans (the
/// registry is process-wide) cannot alter the pipelines these facts construct.
/// </summary>
public class GeneratedVoidPlanExecutionTests
{
    [ExcludeFromDiscovery]
    public sealed class PlannedCommand : ICommand { }

    [ExcludeFromDiscovery]
    public sealed class PlannedCommandHandler : ICommandHandler<PlannedCommand>
    {
        public ValueTask HandleAsync(PlannedCommand command, ErgosfareContext context)
        {
            context.Set("plannedRan", true);
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task PlannedDispatch_RunsTheHandler_WithCallerVisibleItems()
    {
        GeneratedDispatchRoots.AddVoidPlan<PlannedCommand, PlannedCommandHandler>();

        var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c => c.Register<PlannedCommandHandler>()))
            .BuildServiceProvider();
        await using var _ = provider;

        var mediator = provider.GetRequiredService<ICommandMediator>();
        var settings = new CommandMediationSettings();
        settings.Items["keep"] = "me";

        await mediator.SendAsync(new PlannedCommand(), settings);

        Assert.Equal("me", settings.Items["keep"]);
        Assert.Equal(true, settings.Items["plannedRan"]);
    }

    [ExcludeFromDiscovery]
    public sealed class LatePlannedCommand : ICommand { }

    [ExcludeFromDiscovery]
    public sealed class LatePlannedCommandHandler : ICommandHandler<LatePlannedCommand>
    {
        public ValueTask HandleAsync(LatePlannedCommand command, ErgosfareContext context)
            => ValueTask.CompletedTask;
    }

    [ExcludeFromDiscovery]
    public sealed class LatePlannedInterceptor : ICommandPreInterceptor<LatePlannedCommand>
    {
        public ValueTask<LatePlannedCommand> HandleAsync(LatePlannedCommand command, ErgosfareContext context)
        {
            context.Set("lateInterceptorRan", true);
            return ValueTask.FromResult(command);
        }
    }

    [ExcludeFromDiscovery]
    public sealed class MismatchedCommand : ICommand { }

    /// <summary>Registered handler — not the type the plan claims.</summary>
    [ExcludeFromDiscovery]
    public sealed class ActualMismatchedHandler : ICommandHandler<MismatchedCommand>
    {
        public ValueTask HandleAsync(MismatchedCommand command, ErgosfareContext context)
        {
            context.Set("actualRan", true);
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>The plan's claimed handler; never registered anywhere.</summary>
    [ExcludeFromDiscovery]
    public sealed class ClaimedMismatchedHandler : ICommandHandler<MismatchedCommand>
    {
        public ValueTask HandleAsync(MismatchedCommand command, ErgosfareContext context)
            => ValueTask.CompletedTask;
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task PlanClaimingTheWrongHandler_StillDispatchesTheRegisteredOne()
    {
        GeneratedDispatchRoots.AddVoidPlan<MismatchedCommand, ClaimedMismatchedHandler>();

        var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c => c.Register<ActualMismatchedHandler>()))
            .BuildServiceProvider();
        await using var _ = provider;

        var mediator = provider.GetRequiredService<ICommandMediator>();
        var settings = new CommandMediationSettings();

        await mediator.SendAsync(new MismatchedCommand(), settings);

        Assert.Equal(true, settings.Items["actualRan"]);
    }
}
