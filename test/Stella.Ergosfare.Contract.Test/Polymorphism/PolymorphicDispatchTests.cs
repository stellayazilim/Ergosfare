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
using Stella.Ergosfare.Generated;

namespace Stella.Ergosfare.Contract.Test.Polymorphism;

/// <summary>
/// How the registry matches participants that were written against a supertype of the
/// dispatched message.
/// </summary>
/// <remarks>
/// Every supertype here is declared by this class. Registering an interceptor against a
/// module marker (<c>ICommand</c>, <c>IEvent</c>) would attach it to every pipeline in the
/// process — the registry is process-wide — and quietly break unrelated test classes.
/// </remarks>
public sealed class PolymorphicDispatchTests
{
    private const string Key = "contract.polymorphism";

    // --- interceptor matched through an implemented interface -----------------

    /// <summary>A supertype owned by this class, never a module marker.</summary>
    [ExcludeFromDiscovery]
    public interface IAudited : ICommand;

    [DiscoveryKey(Key)]
    public sealed class Withdraw : IAudited;

    [DiscoveryKey(Key)]
    public sealed class WithdrawHandler : ICommandHandler<Withdraw>
    {
        public ValueTask HandleAsync(Withdraw command, IExecutionContext context)
        {
            context.Mark("handler");
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>Registered against the interface, not the concrete command.</summary>
    [DiscoveryKey(Key)]
    public sealed class AuditedPre : ICommandPreInterceptor<IAudited>
    {
        public ValueTask<IAudited> HandleAsync(IAudited command, IExecutionContext context)
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

    /// <summary>Registered as a message in its own right.</summary>
    [DiscoveryKey(Key)]
    public sealed class DepositEntry : LedgerEntry;

    /// <summary>Never registered: only the base-typed handler knows about this shape.</summary>
    [ExcludeFromDiscovery]
    public sealed class TransferEntry : LedgerEntry;

    [DiscoveryKey(Key)]
    public sealed class LedgerEntryHandler : ICommandHandler<LedgerEntry>
    {
        public ValueTask HandleAsync(LedgerEntry command, IExecutionContext context)
        {
            command.SeenType = command.GetType().Name;
            context.Mark("handler:base");
            return ValueTask.CompletedTask;
        }
    }

    // --- event handlers matched directly and through an interface -------------

    /// <inheritdoc cref="IAudited"/>
    [ExcludeFromDiscovery]
    public interface IShipmentEvent : IEvent;

    [DiscoveryKey(Key)]
    public sealed class ShipmentDispatched : IShipmentEvent;

    /// <summary>Direct handler; its type name sorts before the other direct one.</summary>
    [DiscoveryKey(Key)]
    public sealed class ShipmentAuditHandler : IEventHandler<ShipmentDispatched>
    {
        public ValueTask HandleAsync(ShipmentDispatched @event, IExecutionContext context)
        {
            context.Mark("direct:audit");
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>Direct handler; its type name sorts after the other direct one.</summary>
    [DiscoveryKey(Key)]
    public sealed class ShipmentNotifyHandler : IEventHandler<ShipmentDispatched>
    {
        public ValueTask HandleAsync(ShipmentDispatched @event, IExecutionContext context)
        {
            context.Mark("direct:notify");
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>Matched only through the interface the event implements.</summary>
    [DiscoveryKey(Key)]
    public sealed class ShipmentInterfaceHandler : IEventHandler<IShipmentEvent>
    {
        public ValueTask HandleAsync(IShipmentEvent @event, IExecutionContext context)
        {
            context.Mark("indirect:interface");
            return ValueTask.CompletedTask;
        }
    }

    private static ServiceProvider CreateProvider()
        => new ServiceCollection()
            .AddErgosfare(options => options
                .AddCommandModule(commands => commands.RegisterGenerated(Key))
                .AddEventModule(events => events.RegisterGenerated(Key)))
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
    public async Task A_base_typed_handler_serves_a_derived_message_that_is_not_registered_itself()
    {
        await using var provider = CreateProvider();
        var recorder = new PipelineRecorder();
        var command = new TransferEntry();

        await provider.GetRequiredService<ICommandMediator>().SendAsync(command, recorder.Commands());

        recorder.AssertStages("handler:base");
        Assert.Equal(nameof(TransferEntry), command.SeenType);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_base_typed_handler_does_not_serve_a_derived_message_registered_in_its_own_right()
    {
        await using var provider = CreateProvider();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        // Covariant command dispatch is resolution-time only: once the derived type has a
        // descriptor of its own, the base-typed handler is an indirect handler there and
        // single-handler mediation never considers it. See the README.
        var thrown = await Assert.ThrowsAsync<NoHandlerFoundException>(
            async () => await mediator.SendAsync(new DepositEntry()));

        Assert.Contains(nameof(DepositEntry), thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task Publish_runs_direct_handlers_before_interface_matched_ones()
    {
        await using var provider = CreateProvider();
        var recorder = new PipelineRecorder();

        await provider.GetRequiredService<IEventMediator>()
            .PublishAsync(new ShipmentDispatched(), recorder.Events());

        recorder.AssertStages("direct:audit", "direct:notify", "indirect:interface");
    }
}
