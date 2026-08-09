using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Contract.Test.Harness;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Generated;

namespace Stella.Ergosfare.Contract.Test.Handlers;

/// <summary>
/// What a dispatch does when two main handlers claim the same message. Single-handler
/// mediation is the only mediation a command has, so this is a dispatch-time failure
/// rather than a registration-time one — the registry accepts both handlers and the
/// message stays broken for every container in the process.
/// </summary>
/// <remarks>
/// These types stay keyed: a message with two handlers is disqualified from every
/// compile-time plan by the sole-handler gate, so both axes reach the reflective mediation
/// strategies. What the two axes do differ in is where the second descriptor comes from —
/// the generator's pre-computed catalog on one side, reflective descriptor construction on
/// the other.
/// </remarks>
public abstract class MultipleMainHandlerContract
{
    /// <summary>The discovery key this area registers under.</summary>
    protected const string Key = "contract.multi";

    /// <summary>Marks itself so a run that reached a handler can be told from one that did not.</summary>
    [ExcludeFromDiscovery]
    public abstract class ContestedVoidHandlerBase<TCommand>(string slot) : ICommandHandler<TCommand>
        where TCommand : class, ICommand
    {
        /// <inheritdoc />
        public ValueTask HandleAsync(TCommand command, IExecutionContext context)
        {
            context.Mark(slot);
            return ValueTask.CompletedTask;
        }
    }

    /// <inheritdoc cref="ContestedVoidHandlerBase{TCommand}"/>
    [ExcludeFromDiscovery]
    public abstract class ContestedResultHandlerBase<TCommand>(string slot) : ICommandHandler<TCommand, string>
        where TCommand : class, ICommand<string>
    {
        /// <inheritdoc />
        public ValueTask<string> HandleAsync(TCommand command, IExecutionContext context)
        {
            context.Mark(slot);
            return ValueTask.FromResult(slot);
        }
    }

    /// <summary>A container with this axis' contested messages registered its own way.</summary>
    protected abstract ServiceProvider CreateProvider();

    /// <summary>The void command two handlers claim.</summary>
    protected abstract ICommand NewContestedCommand();

    /// <summary>The string-result command two handlers claim.</summary>
    protected abstract ICommand<string> NewContestedResultCommand();

    /// <summary>The simple name of the contested void command's type.</summary>
    protected abstract string ContestedCommandName { get; }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task Two_main_handlers_for_one_void_command_fail_the_dispatch_with_MultipleHandlerFoundException()
    {
        await using var provider = CreateProvider();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        await Assert.ThrowsAsync<MultipleHandlerFoundException>(
            async () => await mediator.SendAsync(NewContestedCommand()));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task Two_main_handlers_for_one_result_command_fail_the_dispatch_with_MultipleHandlerFoundException()
    {
        await using var provider = CreateProvider();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        await Assert.ThrowsAsync<MultipleHandlerFoundException>(
            async () => await mediator.SendAsync(NewContestedResultCommand()));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task The_failure_names_the_message_type_and_how_many_handlers_claimed_it()
    {
        await using var provider = CreateProvider();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        var thrown = await Assert.ThrowsAsync<MultipleHandlerFoundException>(
            async () => await mediator.SendAsync(NewContestedCommand()));

        Assert.Contains(ContestedCommandName, thrown.Message, StringComparison.Ordinal);
        Assert.Contains("2", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task Neither_handler_runs_when_two_claim_the_same_message()
    {
        await using var provider = CreateProvider();
        var recorder = new PipelineRecorder { Label = GetType().Name };
        var mediator = provider.GetRequiredService<ICommandMediator>();

        await Assert.ThrowsAsync<MultipleHandlerFoundException>(
            async () => await mediator.SendAsync(NewContestedCommand(), recorder.Commands()));

        // The count is checked before anything is resolved: the dispatch does not pick one
        // handler, and it does not run both either.
        Assert.Empty(recorder.Stages);
    }
}

/// <summary>The contested messages registered through generated descriptors.</summary>
public static class KeyedContestedTypes
{
    /// <summary>Void command two main handlers claim.</summary>
    [DiscoveryKey("contract.multi")]
    public sealed class ContestedCommand : ICommand;

    /// <inheritdoc />
    [DiscoveryKey("contract.multi")]
    public sealed class ContestedCommandFirstHandler()
        : MultipleMainHandlerContract.ContestedVoidHandlerBase<ContestedCommand>("handler:first");

    /// <inheritdoc />
    [DiscoveryKey("contract.multi")]
    public sealed class ContestedCommandSecondHandler()
        : MultipleMainHandlerContract.ContestedVoidHandlerBase<ContestedCommand>("handler:second");

    /// <summary>String-result command two main handlers claim.</summary>
    [DiscoveryKey("contract.multi")]
    public sealed class ContestedResultCommand : ICommand<string>;

    /// <inheritdoc />
    [DiscoveryKey("contract.multi")]
    public sealed class ContestedResultCommandFirstHandler()
        : MultipleMainHandlerContract.ContestedResultHandlerBase<ContestedResultCommand>("handler:first");

    /// <inheritdoc />
    [DiscoveryKey("contract.multi")]
    public sealed class ContestedResultCommandSecondHandler()
        : MultipleMainHandlerContract.ContestedResultHandlerBase<ContestedResultCommand>("handler:second");
}

/// <summary>The same shapes, hidden from the generator so <c>Register&lt;T&gt;()</c> is reflective.</summary>
public static class FallbackContestedTypes
{
    /// <inheritdoc cref="KeyedContestedTypes.ContestedCommand"/>
    [ExcludeFromDiscovery]
    public sealed class ContestedCommand : ICommand;

    /// <inheritdoc />
    [ExcludeFromDiscovery]
    public sealed class ContestedCommandFirstHandler()
        : MultipleMainHandlerContract.ContestedVoidHandlerBase<ContestedCommand>("handler:first");

    /// <inheritdoc />
    [ExcludeFromDiscovery]
    public sealed class ContestedCommandSecondHandler()
        : MultipleMainHandlerContract.ContestedVoidHandlerBase<ContestedCommand>("handler:second");

    /// <inheritdoc cref="KeyedContestedTypes.ContestedResultCommand"/>
    [ExcludeFromDiscovery]
    public sealed class ContestedResultCommand : ICommand<string>;

    /// <inheritdoc />
    [ExcludeFromDiscovery]
    public sealed class ContestedResultCommandFirstHandler()
        : MultipleMainHandlerContract.ContestedResultHandlerBase<ContestedResultCommand>("handler:first");

    /// <inheritdoc />
    [ExcludeFromDiscovery]
    public sealed class ContestedResultCommandSecondHandler()
        : MultipleMainHandlerContract.ContestedResultHandlerBase<ContestedResultCommand>("handler:second");
}

/// <summary>The contested-message contract under generated (keyed) registration.</summary>
public sealed class GeneratedRegistrationMultipleMainHandlerTests : MultipleMainHandlerContract
{
    /// <inheritdoc />
    protected override ServiceProvider CreateProvider()
        => new ServiceCollection()
            .AddErgosfare(options => options
                .AddCommandModule(commands => commands.RegisterGenerated(Key)))
            .BuildServiceProvider();

    /// <inheritdoc />
    protected override ICommand NewContestedCommand() => new KeyedContestedTypes.ContestedCommand();

    /// <inheritdoc />
    protected override ICommand<string> NewContestedResultCommand()
        => new KeyedContestedTypes.ContestedResultCommand();

    /// <inheritdoc />
    protected override string ContestedCommandName => nameof(KeyedContestedTypes.ContestedCommand);
}

/// <summary>The same contract under explicit runtime registration.</summary>
public sealed class RuntimeRegistrationMultipleMainHandlerTests : MultipleMainHandlerContract
{
    /// <inheritdoc />
    protected override ServiceProvider CreateProvider()
        => new ServiceCollection()
            .AddErgosfare(options => options
                .AddCommandModule(commands => commands
                    .Register<FallbackContestedTypes.ContestedCommandFirstHandler>()
                    .Register<FallbackContestedTypes.ContestedCommandSecondHandler>()
                    .Register<FallbackContestedTypes.ContestedResultCommandFirstHandler>()
                    .Register<FallbackContestedTypes.ContestedResultCommandSecondHandler>()))
            .BuildServiceProvider();

    /// <inheritdoc />
    protected override ICommand NewContestedCommand() => new FallbackContestedTypes.ContestedCommand();

    /// <inheritdoc />
    protected override ICommand<string> NewContestedResultCommand()
        => new FallbackContestedTypes.ContestedResultCommand();

    /// <inheritdoc />
    protected override string ContestedCommandName => nameof(FallbackContestedTypes.ContestedCommand);
}
