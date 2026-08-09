using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Contract.Test.Harness;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Abstractions.Registry;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Generated;

namespace Stella.Ergosfare.Contract.Test.Runtime;

/// <summary>
/// Registering into a warm container: after the pipeline for a message has already run
/// once, adding an interceptor through the public registry must change the next dispatch —
/// under generated registration exactly as under runtime registration.
/// </summary>
/// <remarks>
/// Serialized against the rest of the suite: these are the only scenarios that mutate the
/// process-wide registry after a container is live, and the version bump they cause
/// invalidates every container's cached pipeline.
/// </remarks>
[Collection(RegistryMutationCollection.Name)]
public sealed class RuntimeRegistrationMutationTests
{
    private const string Key = "contract.mutation";

    // --- generated axis --------------------------------------------------------

    [DiscoveryKey(Key)]
    public sealed class GeneratedTarget : ICommand;

    [DiscoveryKey(Key)]
    public sealed class GeneratedTargetHandler : ICommandHandler<GeneratedTarget>
    {
        public ValueTask HandleAsync(GeneratedTarget command, IExecutionContext context)
        {
            context.Mark("handler");
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Excluded from discovery so no registration path can install it early: the scenario
    /// must observe a pipeline that genuinely lacked it before the mutation.
    /// </summary>
    [ExcludeFromDiscovery]
    public sealed class GeneratedLatePre : ICommandPreInterceptor<GeneratedTarget>
    {
        public ValueTask<GeneratedTarget> HandleAsync(GeneratedTarget command, IExecutionContext context)
        {
            context.Mark("late");
            return ValueTask.FromResult(command);
        }
    }

    // --- runtime axis ----------------------------------------------------------

    [ExcludeFromDiscovery]
    public sealed class RuntimeTarget : ICommand;

    [ExcludeFromDiscovery]
    public sealed class RuntimeTargetHandler : ICommandHandler<RuntimeTarget>
    {
        public ValueTask HandleAsync(RuntimeTarget command, IExecutionContext context)
        {
            context.Mark("handler");
            return ValueTask.CompletedTask;
        }
    }

    /// <inheritdoc cref="GeneratedLatePre"/>
    [ExcludeFromDiscovery]
    public sealed class RuntimeLatePre : ICommandPreInterceptor<RuntimeTarget>
    {
        public ValueTask<RuntimeTarget> HandleAsync(RuntimeTarget command, IExecutionContext context)
        {
            context.Mark("late");
            return ValueTask.FromResult(command);
        }
    }

    // --- unresolvable late registration ----------------------------------------

    /// <summary>
    /// Its own message type: the registry never forgets, so the scenario below leaves this
    /// pipeline permanently broken for the rest of the process.
    /// </summary>
    [ExcludeFromDiscovery]
    public sealed class PoisonTarget : ICommand;

    [ExcludeFromDiscovery]
    public sealed class PoisonTargetHandler : ICommandHandler<PoisonTarget>
    {
        public ValueTask HandleAsync(PoisonTarget command, IExecutionContext context) => ValueTask.CompletedTask;
    }

    /// <summary>Registered nowhere in DI, to pin what an unresolvable late type does.</summary>
    [ExcludeFromDiscovery]
    public sealed class UnresolvableLatePre : ICommandPreInterceptor<PoisonTarget>
    {
        public ValueTask<PoisonTarget> HandleAsync(PoisonTarget command, IExecutionContext context)
            => ValueTask.FromResult(command);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_generated_pipeline_picks_up_an_interceptor_registered_after_it_ran()
    {
        await using var provider = new ServiceCollection()
            .AddTransient<GeneratedLatePre>()
            .AddErgosfare(options => options.AddCommandModule(commands => commands.RegisterGenerated(Key)))
            .BuildServiceProvider();

        var mediator = provider.GetRequiredService<ICommandMediator>();

        var before = new PipelineRecorder();
        await mediator.SendAsync(new GeneratedTarget(), before.Commands());
        before.AssertStages("handler");

        provider.GetRequiredService<IMessageRegistry>().Register(typeof(GeneratedLatePre));

        var after = new PipelineRecorder();
        await mediator.SendAsync(new GeneratedTarget(), after.Commands());
        after.AssertStages("late", "handler");
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_runtime_registered_pipeline_picks_up_an_interceptor_registered_after_it_ran()
    {
        await using var provider = new ServiceCollection()
            .AddTransient<RuntimeLatePre>()
            .AddErgosfare(options => options
                .AddCommandModule(commands => commands.Register<RuntimeTargetHandler>()))
            .BuildServiceProvider();

        var mediator = provider.GetRequiredService<ICommandMediator>();

        var before = new PipelineRecorder();
        await mediator.SendAsync(new RuntimeTarget(), before.Commands());
        before.AssertStages("handler");

        provider.GetRequiredService<IMessageRegistry>().Register(typeof(RuntimeLatePre));

        var after = new PipelineRecorder();
        await mediator.SendAsync(new RuntimeTarget(), after.Commands());
        after.AssertStages("late", "handler");
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_late_registered_interceptor_the_container_cannot_resolve_fails_the_next_dispatch()
    {
        await using var provider = new ServiceCollection()
            .AddErgosfare(options => options
                .AddCommandModule(commands => commands.Register<PoisonTargetHandler>()))
            .BuildServiceProvider();

        var mediator = provider.GetRequiredService<ICommandMediator>();
        await mediator.SendAsync(new PoisonTarget());

        // The registry accepts the type, but the container was built without it: the
        // pipeline that now includes it cannot be constructed. See the README.
        provider.GetRequiredService<IMessageRegistry>().Register(typeof(UnresolvableLatePre));

        var thrown = await Assert.ThrowsAsync<UnresolvableParticipantException>(
            async () => await mediator.SendAsync(new PoisonTarget()));

        Assert.Contains(nameof(UnresolvableLatePre), thrown.Message, StringComparison.Ordinal);
        Assert.Equal(typeof(PoisonTarget), thrown.MessageType);
        Assert.Equal(typeof(UnresolvableLatePre), thrown.ParticipantType);
    }

    // --- recovery from an unresolvable late registration -----------------------

    /// <summary>
    /// The recovery scenario keeps types of its own. Sharing <see cref="PoisonTarget"/>
    /// would make each scenario's outcome depend on which ran first: the registry is
    /// process-wide and never forgets, so whichever registered the unresolvable
    /// interceptor first would break the other's opening dispatch.
    /// </summary>
    [ExcludeFromDiscovery]
    public sealed class RecoveryTarget : ICommand;

    /// <inheritdoc cref="RecoveryTarget"/>
    [ExcludeFromDiscovery]
    public sealed class RecoveryTargetHandler : ICommandHandler<RecoveryTarget>
    {
        public ValueTask HandleAsync(RecoveryTarget command, IExecutionContext context)
        {
            context.Mark("handler");
            return ValueTask.CompletedTask;
        }
    }

    /// <inheritdoc cref="RecoveryTarget"/>
    [ExcludeFromDiscovery]
    public sealed class UnresolvableRecoveryPre : ICommandPreInterceptor<RecoveryTarget>
    {
        public ValueTask<RecoveryTarget> HandleAsync(RecoveryTarget command, IExecutionContext context)
        {
            context.Mark("late");
            return ValueTask.FromResult(command);
        }
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_container_that_can_resolve_the_late_participant_builds_the_pipeline_the_broken_one_could_not()
    {
        await using var missing = new ServiceCollection()
            .AddErgosfare(options => options
                .AddCommandModule(commands => commands.Register<RecoveryTargetHandler>()))
            .BuildServiceProvider();

        await missing.GetRequiredService<ICommandMediator>().SendAsync(new RecoveryTarget());

        missing.GetRequiredService<IMessageRegistry>().Register(typeof(UnresolvableRecoveryPre));

        await Assert.ThrowsAsync<UnresolvableParticipantException>(
            async () => await missing.GetRequiredService<ICommandMediator>().SendAsync(new RecoveryTarget()));

        // The registry entry is permanent, so registering the participant with a container
        // is the only way back — and it has to work. Nothing about the failed build is
        // cached, so this container reaches the same pipeline the broken one could not,
        // interceptor included.
        await using var repaired = new ServiceCollection()
            .AddErgosfare(options => options
                .AddCommandModule(commands => commands
                    .Register<RecoveryTargetHandler>()
                    .Register<UnresolvableRecoveryPre>()))
            .BuildServiceProvider();

        var recorder = new PipelineRecorder();
        await repaired.GetRequiredService<ICommandMediator>().SendAsync(new RecoveryTarget(), recorder.Commands());

        recorder.AssertStages("late", "handler");
    }
}

/// <summary>
/// Serializes the registry-mutating scenarios against each other. The registry is
/// process-wide; letting these run beside anything else invites unrelated flakes.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RegistryMutationCollection
{
    /// <summary>The collection name.</summary>
    public const string Name = "registry-mutation";
}
