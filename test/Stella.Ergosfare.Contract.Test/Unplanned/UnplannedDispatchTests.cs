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

// Fresh area-local types. The hand-registered ones are excluded from discovery — explicit Register<T>() must still select them; the partial
// pipeline is discoverable, so its plan exists and the container is what falls short.

/// <summary>Void command whose only handler explicit selection collects.</summary>
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

/// <summary>String command whose only handler explicit selection collects.</summary>
[ExcludeFromDiscovery]
public sealed class HandRegisteredResultCommand : ICommand<string>;

/// <inheritdoc cref="HandRegisteredResultCommand"/>
[ExcludeFromDiscovery]
public sealed class HandRegisteredResultCommandHandler : ICommandHandler<HandRegisteredResultCommand, string>
{
    /// <inheritdoc />
    public ValueTask<string> HandleAsync(HandRegisteredResultCommand command, ErgosfareContext context)
        => ValueTask.FromResult("delivered");
}

/// <summary>Event whose only subscriber explicit selection collects.</summary>
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
/// Explicit selections produce executable plans even for excluded types. A deliberately
/// incomplete runtime catalog cannot execute a different composition from its compiled plan.
/// </summary>
public sealed class UnplannedDispatchTests
{
    [Fact]
    [Trait("Category", "Contract")]
    public async Task An_explicitly_selected_excluded_handler_serves_void_send()
    {
        await using var provider = new ServiceCollection()
            .AddErgosfare(options => options
                .AddCommandModule(commands => commands.Register<HandRegisteredCommandHandler>()))
            .BuildServiceProvider();

        var mediator = provider.GetRequiredService<ICommandMediator>();

        await mediator.SendAsync(new HandRegisteredCommand());
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task An_explicitly_selected_excluded_handler_serves_result_send()
    {
        await using var provider = new ServiceCollection()
            .AddErgosfare(options => options
                .AddCommandModule(commands => commands.Register<HandRegisteredResultCommandHandler>()))
            .BuildServiceProvider();

        var mediator = provider.GetRequiredService<ICommandMediator>();

        Assert.Equal("delivered", await mediator.SendAsync(new HandRegisteredResultCommand()));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task An_explicitly_selected_excluded_subscriber_receives_publish()
    {
        await using var provider = new ServiceCollection()
            .AddErgosfare(options => options
                .AddEventModule(events => events.Register<HandRegisteredEventHandler>()))
            .BuildServiceProvider();

        var mediator = provider.GetRequiredService<IEventMediator>();

        await mediator.PublishAsync(new HandRegisteredEvent());
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
