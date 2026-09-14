using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Contract.Test.Harness;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Events.Abstractions;
using Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection;

namespace Stella.Ergosfare.Contract.Test.Polymorphism;

// Top-level and unkeyed so the generator can model what it models; every supertype here is
// declared by this area, never a module marker — a marker-wide interceptor would attach to
// every pipeline in the process.

// --- interceptor matched through an implemented interface -----------------

/// <summary>A supertype owned by this area, never a module marker.</summary>
[ExcludeFromDiscovery]
public interface IAudited : ICommand;

/// <summary>Command reached covariantly by the interceptor below.</summary>
public sealed class Withdraw : IAudited;

/// <inheritdoc />
public sealed class WithdrawHandler : ICommandHandler<Withdraw>
{
    /// <inheritdoc />
    public ValueTask HandleAsync(Withdraw command, ErgosfareContext context)
    {
        context.Mark("handler");
        return ValueTask.CompletedTask;
    }
}

/// <summary>Registered against the interface, not the concrete command.</summary>
public sealed class AuditedPre : ICommandPreInterceptor<IAudited>
{
    /// <inheritdoc />
    public ValueTask<IAudited> HandleAsync(IAudited command, ErgosfareContext context)
    {
        context.Mark("pre:audited");
        return ValueTask.FromResult(command);
    }
}

// --- handler matched through a base class ---------------------------------

/// <summary>The base the handler is written against.</summary>
[ExcludeFromDiscovery]
public abstract class LedgerEntry : ICommand
{
    /// <summary>The runtime type the base-typed handler observed.</summary>
    public string? SeenType;
}

/// <summary>Discovered as a message in its own right.</summary>
public sealed class DepositEntry : LedgerEntry;

/// <summary>Never discovered: only the base-typed handler knows about this shape.</summary>
[ExcludeFromDiscovery]
public sealed class TransferEntry : LedgerEntry;

/// <inheritdoc />
public sealed class LedgerEntryHandler : ICommandHandler<LedgerEntry>
{
    /// <inheritdoc />
    public ValueTask HandleAsync(LedgerEntry command, ErgosfareContext context)
    {
        command.SeenType = command.GetType().Name;
        context.Mark("handler:base");
        return ValueTask.CompletedTask;
    }
}

/// <summary>Discovered and handled in its own right, under a handled base.</summary>
public sealed class WithdrawalEntry : LedgerEntry;

/// <summary>The direct claim on <see cref="WithdrawalEntry"/>; the base handler is the other one.</summary>
public sealed class WithdrawalEntryHandler : ICommandHandler<WithdrawalEntry>
{
    /// <inheritdoc />
    public ValueTask HandleAsync(WithdrawalEntry command, ErgosfareContext context)
    {
        command.SeenType = command.GetType().Name;
        context.Mark("handler:direct");
        return ValueTask.CompletedTask;
    }
}

// --- two covariant claimants and no direct one ------------------------------

/// <summary>A second handled supertype, so a message can be claimed covariantly twice.</summary>
[ExcludeFromDiscovery]
public interface IArchivedEntry : ICommand;

/// <summary>Claimed through <see cref="LedgerEntry"/> AND <see cref="IArchivedEntry"/>; no direct handler.</summary>
public sealed class ArchivedTransferEntry : LedgerEntry, IArchivedEntry;

/// <summary>The second covariant claimant.</summary>
public sealed class ArchivedEntryHandler : ICommandHandler<IArchivedEntry>
{
    /// <inheritdoc />
    public ValueTask HandleAsync(IArchivedEntry command, ErgosfareContext context)
    {
        context.Mark("handler:archived");
        return ValueTask.CompletedTask;
    }
}

// --- event handlers matched directly and through an interface -------------

/// <inheritdoc cref="IAudited"/>
[ExcludeFromDiscovery]
public interface IShipmentEvent : IEvent;

/// <summary>Event claimed directly twice and covariantly once.</summary>
public sealed class ShipmentDispatched : IShipmentEvent;

/// <summary>Direct handler; its type name sorts before the other direct one.</summary>
public sealed class ShipmentAuditHandler : IEventHandler<ShipmentDispatched>
{
    /// <inheritdoc />
    public ValueTask HandleAsync(ShipmentDispatched @event, ErgosfareContext context)
    {
        context.Mark("direct:audit");
        return ValueTask.CompletedTask;
    }
}

/// <summary>Direct handler; its type name sorts after the other direct one.</summary>
public sealed class ShipmentNotifyHandler : IEventHandler<ShipmentDispatched>
{
    /// <inheritdoc />
    public ValueTask HandleAsync(ShipmentDispatched @event, ErgosfareContext context)
    {
        context.Mark("direct:notify");
        return ValueTask.CompletedTask;
    }
}

/// <summary>Matched only through the interface the event implements.</summary>
public sealed class ShipmentInterfaceHandler : IEventHandler<IShipmentEvent>
{
    /// <inheritdoc />
    public ValueTask HandleAsync(IShipmentEvent @event, ErgosfareContext context)
    {
        context.Mark("indirect:interface");
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// How the plan lane treats participants written against a supertype of the dispatched
/// message. Interceptors keep their covariance inside compiled plans, and so do broadcast
/// subscribers. On the send side the direct level of the main-handler ladder survives — a
/// message with its own handler is planned, covariant claims and all — but a message
/// claimed <em>only</em> covariantly has no plan, and a planless send fails loudly; the
/// composite plan that would restore that half of the ladder is queued, not compiled.
/// </summary>
public sealed class PolymorphicDispatchTests
{
    private static ServiceProvider CreateProvider()
        => new ServiceCollection()
            .AddErgosfare(options => options
                .AddCommandModule(commands => commands
                    .Register<WithdrawHandler>()
                    .Register<AuditedPre>()
                    .Register<LedgerEntryHandler>()
                    .Register<WithdrawalEntryHandler>()
                    .Register<ArchivedEntryHandler>())
                .AddEventModule(events => events
                    .Register<ShipmentAuditHandler>()
                    .Register<ShipmentNotifyHandler>()
                    .Register<ShipmentInterfaceHandler>()))
            .BuildServiceProvider();

    [Fact]
    [Trait("Category", "Contract")]
    public async Task An_interceptor_registered_against_an_implemented_interface_joins_the_pipeline()
    {
        await using var provider = CreateProvider();
        var recorder = new PipelineRecorder();

        await provider.GetRequiredService<ICommandMediator>().SendAsync(new Withdraw(), recorder.Commands());

        recorder.AssertStages("pre:audited", "handler");
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_message_claimed_only_through_its_base_fails_the_dispatch_unplanned()
    {
        await using var provider = CreateProvider();
        var recorder = new PipelineRecorder();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        // The covariant send ladder is unplannable today: a base-typed main-handler claim
        // disqualifies the send plan, and a planless send fails loudly instead of running a
        // degraded lane. The base handler never runs.
        var thrown = await Assert.ThrowsAsync<UnplannedDispatchException>(
            async () => await mediator.SendAsync(new TransferEntry(), recorder.Commands()));

        Assert.Equal(UnplannedDispatchReason.NoCompiledPlan, thrown.Reason);
        Assert.Empty(recorder.Stages);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_discovered_message_claimed_only_through_its_base_fails_the_same_way()
    {
        await using var provider = CreateProvider();
        var recorder = new PipelineRecorder();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        // Whether the message has a descriptor of its own no longer matters: the claim is
        // covariant either way, and a covariant claim is what disqualifies the plan.
        var thrown = await Assert.ThrowsAsync<UnplannedDispatchException>(
            async () => await mediator.SendAsync(new DepositEntry(), recorder.Commands()));

        Assert.Equal(UnplannedDispatchReason.NoCompiledPlan, thrown.Reason);
        Assert.Empty(recorder.Stages);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_direct_handler_beats_a_base_typed_one_claiming_the_same_message()
    {
        await using var provider = CreateProvider();
        var recorder = new PipelineRecorder();
        var command = new WithdrawalEntry();

        // The main-handler priority ladder survives inside the plan when the direct level
        // is decided: the generator bakes the direct handler in — the covariant claim is a
        // fallback, not a competitor — so the message's own handler serves it and the
        // base-typed one never runs. Only a message with NO direct handler is unplanned.
        await provider.GetRequiredService<ICommandMediator>().SendAsync(command, recorder.Commands());

        recorder.AssertStages("handler:direct");
        Assert.Equal(nameof(WithdrawalEntry), command.SeenType);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task Publish_runs_direct_handlers_before_interface_matched_ones()
    {
        await using var provider = CreateProvider();
        var recorder = new PipelineRecorder();

        // Broadcast keeps its covariance: the compiled broadcast plan includes the
        // interface-matched subscriber, after the direct ones.
        await provider.GetRequiredService<IEventMediator>()
            .PublishAsync(new ShipmentDispatched(), recorder.Events());

        recorder.AssertStages("direct:audit", "direct:notify", "indirect:interface");
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task Two_covariant_claimants_with_no_direct_handler_fail_the_dispatch()
    {
        await using var provider = CreateProvider();
        var recorder = new PipelineRecorder();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        // Within one level the ladder has no tiebreaker: with no direct handler to win,
        // two covariant claimants are a contest and the dispatch fails rather than
        // picking one. The contest is counted even though no plan exists — the throw path
        // inspects the participants.
        var thrown = await Assert.ThrowsAsync<MultipleHandlerFoundException>(
            async () => await mediator.SendAsync(new ArchivedTransferEntry(), recorder.Commands()));

        Assert.Contains(nameof(ArchivedTransferEntry), thrown.Message, StringComparison.Ordinal);
        Assert.Contains("2", thrown.Message, StringComparison.Ordinal);

        // Counted before anything resolves: neither claimant runs.
        Assert.Empty(recorder.Stages);
    }
}
