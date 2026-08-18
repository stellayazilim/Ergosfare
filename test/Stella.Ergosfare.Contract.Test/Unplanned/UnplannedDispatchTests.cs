using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Events.Abstractions;
using Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection;

namespace Stella.Ergosfare.Contract.Test.Unplanned;

// Fresh area-local types. The hand-registered ones are excluded from discovery — the
// generator never sees them, which is exactly the situation being pinned; the partial
// pipeline is discoverable, so its plan exists and the container is what falls short.

/// <summary>Void command whose only handler the generator never saw.</summary>
[ExcludeFromDiscovery]
public sealed class HandRegisteredCommand : ICommand;

/// <inheritdoc cref="HandRegisteredCommand"/>
[ExcludeFromDiscovery]
public sealed class HandRegisteredCommandHandler : ICommandHandler<HandRegisteredCommand>
{
    /// <inheritdoc />
    public ValueTask HandleAsync(HandRegisteredCommand command, ErgosfareContext context)
        => ValueTask.CompletedTask;
}

/// <summary>String command whose only handler the generator never saw.</summary>
[ExcludeFromDiscovery]
public sealed class HandRegisteredResultCommand : ICommand<string>;

/// <inheritdoc cref="HandRegisteredResultCommand"/>
[ExcludeFromDiscovery]
public sealed class HandRegisteredResultCommandHandler : ICommandHandler<HandRegisteredResultCommand, string>
{
    /// <inheritdoc />
    public ValueTask<string> HandleAsync(HandRegisteredResultCommand command, ErgosfareContext context)
        => ValueTask.FromResult("never-delivered");
}

/// <summary>Event whose only subscriber the generator never saw.</summary>
[ExcludeFromDiscovery]
public sealed class HandRegisteredEvent : IEvent;

/// <inheritdoc cref="HandRegisteredEvent"/>
[ExcludeFromDiscovery]
public sealed class HandRegisteredEventHandler : IEventHandler<HandRegisteredEvent>
{
    /// <inheritdoc />
    public ValueTask HandleAsync(HandRegisteredEvent @event, ErgosfareContext context)
        => ValueTask.CompletedTask;
}

/// <summary>Command whose compiled pipeline is a handler and a pre-interceptor.</summary>
public sealed class PartialPipelineCommand : ICommand;

/// <inheritdoc cref="PartialPipelineCommand"/>
public sealed class PartialPipelineCommandHandler : ICommandHandler<PartialPipelineCommand>
{
    /// <inheritdoc />
    public ValueTask HandleAsync(PartialPipelineCommand command, ErgosfareContext context)
        => ValueTask.CompletedTask;
}

/// <summary>The discoverable pre-interceptor the diverged container leaves out.</summary>
public sealed class PartialPipelineCommandPre : ICommandPreInterceptor<PartialPipelineCommand>
{
    /// <inheritdoc />
    public ValueTask<PartialPipelineCommand> HandleAsync(PartialPipelineCommand command, ErgosfareContext context)
        => ValueTask.FromResult(command);
}

/// <summary>
/// The unplanned-dispatch contract from the public surface: nothing is dispatched at run
/// time that was not produced at compile time. A pipeline the generator never saw serves
/// nothing however carefully it is registered by hand, and a container holding only part
/// of a compiled pipeline is refused with the diverged stage named.
/// </summary>
public sealed class UnplannedDispatchTests
{
    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_hand_registered_handler_the_generator_never_saw_serves_no_void_send()
    {
        await using var provider = new ServiceCollection()
            .AddErgosfare(options => options
                .AddCommandModule(commands => commands.Register<HandRegisteredCommandHandler>()))
            .BuildServiceProvider();

        var mediator = provider.GetRequiredService<ICommandMediator>();

        // The handler is registered and resolvable — and serves nothing, because no
        // compiled plan names it. The failure is the plan's absence, not the handler's.
        var thrown = await Assert.ThrowsAsync<UnplannedDispatchException>(
            async () => await mediator.SendAsync(new HandRegisteredCommand()));

        Assert.Equal(UnplannedDispatchReason.NoCompiledPlan, thrown.Reason);
        Assert.Equal(typeof(HandRegisteredCommand), thrown.MessageType);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_hand_registered_handler_the_generator_never_saw_serves_no_result_send()
    {
        await using var provider = new ServiceCollection()
            .AddErgosfare(options => options
                .AddCommandModule(commands => commands.Register<HandRegisteredResultCommandHandler>()))
            .BuildServiceProvider();

        var mediator = provider.GetRequiredService<ICommandMediator>();

        var thrown = await Assert.ThrowsAsync<UnplannedDispatchException>(
            async () => await mediator.SendAsync(new HandRegisteredResultCommand()));

        Assert.Equal(UnplannedDispatchReason.NoCompiledPlan, thrown.Reason);
        Assert.Equal(typeof(HandRegisteredResultCommand), thrown.MessageType);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_publish_whose_only_subscriber_the_generator_never_saw_fails_unplanned()
    {
        await using var provider = new ServiceCollection()
            .AddErgosfare(options => options
                .AddEventModule(events => events.Register<HandRegisteredEventHandler>()))
            .BuildServiceProvider();

        var mediator = provider.GetRequiredService<IEventMediator>();

        // A publish reaching nobody is a no-op; a publish that would reach somebody
        // without a plan is not — the subscriber would silently go unserved.
        var thrown = await Assert.ThrowsAsync<UnplannedDispatchException>(
            async () => await mediator.PublishAsync(new HandRegisteredEvent()));

        Assert.Equal(UnplannedDispatchReason.NoCompiledPlan, thrown.Reason);
        Assert.Equal(typeof(HandRegisteredEvent), thrown.MessageType);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_container_holding_part_of_a_compiled_pipeline_fails_naming_the_diverged_stage()
    {
        // The compiled plan for PartialPipelineCommand is handler plus pre-interceptor.
        // This container registers the handler and leaves the interceptor out, so the live
        // pipeline is not the one the plan was baked against.
        await using var provider = new ServiceCollection()
            .AddErgosfare(options => options
                .AddCommandModule(commands => commands.Register<PartialPipelineCommandHandler>()))
            .BuildServiceProvider();

        var mediator = provider.GetRequiredService<ICommandMediator>();

        var thrown = await Assert.ThrowsAsync<UnplannedDispatchException>(
            async () => await mediator.SendAsync(new PartialPipelineCommand()));

        Assert.Equal(UnplannedDispatchReason.CompositionDiverged, thrown.Reason);
        Assert.Contains("pre-interceptors", thrown.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(PartialPipelineCommandPre), thrown.Message, StringComparison.Ordinal);
    }
}
