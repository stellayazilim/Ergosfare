using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.DispatchRoots;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Command.Test;

/// <summary>
/// Runtime behavior of generated result pipeline plans, installed through the public
/// <see cref="GeneratedDispatchRoots.AddResultPlan{TMessage, TResult, THandler}()"/>
/// surface exactly as generated code would — the result-producing counterpart of
/// <see cref="GeneratedVoidPlanExecutionTests"/>. Helper types are excluded from discovery
/// so assembly scans (the registry is process-wide) cannot alter these pipelines.
/// </summary>
public class GeneratedResultPlanExecutionTests
{
    [ExcludeFromDiscovery]
    public sealed class PlannedEcho : ICommand<string>
    {
        public string Payload { get; init; } = string.Empty;
    }

    [ExcludeFromDiscovery]
    public sealed class PlannedEchoHandler : ICommandHandler<PlannedEcho, string>
    {
        public bool ViaPlanFactory { get; init; }

        public ValueTask<string> HandleAsync(PlannedEcho command, IExecutionContext context)
        {
            context.Set("viaPlanFactory", ViaPlanFactory);
            return ValueTask.FromResult(command.Payload + "!");
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task PlannedResultDispatch_ReturnsTheResult_AndConstructsThroughTheFactory()
    {
        GeneratedDispatchRoots.AddResultPlan<PlannedEcho, string, PlannedEchoHandler>(
            static () => new PlannedEchoHandler { ViaPlanFactory = true });

        var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c => c.Register<PlannedEchoHandler>()))
            .BuildServiceProvider();
        await using var _ = provider;

        var mediator = provider.GetRequiredService<ICommandMediator>();
        var settings = new CommandMediationSettings();

        var result = await mediator.SendAsync(new PlannedEcho { Payload = "hi" }, settings);

        Assert.Equal("hi!", result);
        Assert.Equal(true, settings.Items["viaPlanFactory"]);
    }

    [ExcludeFromDiscovery]
    public sealed class MismatchedEcho : ICommand<string> { }

    /// <summary>Registered handler — not the type the plan claims.</summary>
    [ExcludeFromDiscovery]
    public sealed class ActualMismatchedEchoHandler : ICommandHandler<MismatchedEcho, string>
    {
        public ValueTask<string> HandleAsync(MismatchedEcho command, IExecutionContext context)
            => ValueTask.FromResult("actual");
    }

    /// <summary>The plan's claimed handler; never registered anywhere.</summary>
    [ExcludeFromDiscovery]
    public sealed class ClaimedMismatchedEchoHandler : ICommandHandler<MismatchedEcho, string>
    {
        public ValueTask<string> HandleAsync(MismatchedEcho command, IExecutionContext context)
            => ValueTask.FromResult("claimed");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ResultPlanClaimingTheWrongHandler_StillDispatchesTheRegisteredOne()
    {
        GeneratedDispatchRoots.AddResultPlan<MismatchedEcho, string, ClaimedMismatchedEchoHandler>(
            static () => new ClaimedMismatchedEchoHandler());

        var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c => c.Register<ActualMismatchedEchoHandler>()))
            .BuildServiceProvider();
        await using var _ = provider;

        var mediator = provider.GetRequiredService<ICommandMediator>();

        // The plan claims a handler the registry never saw: the factory must stay unused
        // (its type differs from the registered reference) and the registered handler
        // must run, exactly as the runtime executor would dispatch it.
        var result = await mediator.SendAsync(new MismatchedEcho());

        Assert.Equal("actual", result);
    }
}
