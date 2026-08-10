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
/// Registering into a warm container: a message's pipeline freezes at its first dispatch.
/// A registration made before that dispatch joins the pipeline; one made after it is not
/// observed — under generated registration exactly as under runtime registration.
/// </summary>
/// <remarks>
/// Serialized against the rest of the suite: these scenarios put types into the
/// process-wide registry mid-test, and the freeze-order semantics they pin would blur
/// beside parallel neighbors.
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
    /// must observe a pipeline that genuinely lacked it when it froze.
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

    // --- registration before the freeze -----------------------------------------

    /// <summary>
    /// Its own message type: the positive control must own the first dispatch of the
    /// message it registers into, or another scenario's freeze would decide its outcome.
    /// </summary>
    [ExcludeFromDiscovery]
    public sealed class EarlyTarget : ICommand;

    /// <inheritdoc cref="EarlyTarget"/>
    [ExcludeFromDiscovery]
    public sealed class EarlyTargetHandler : ICommandHandler<EarlyTarget>
    {
        public ValueTask HandleAsync(EarlyTarget command, IExecutionContext context)
        {
            context.Mark("handler");
            return ValueTask.CompletedTask;
        }
    }

    /// <inheritdoc cref="EarlyTarget"/>
    [ExcludeFromDiscovery]
    public sealed class EarlyPre : ICommandPreInterceptor<EarlyTarget>
    {
        public ValueTask<EarlyTarget> HandleAsync(EarlyTarget command, IExecutionContext context)
        {
            context.Mark("early");
            return ValueTask.FromResult(command);
        }
    }

    // --- unresolvable participant ----------------------------------------------

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

    /// <summary>Registered nowhere in DI, to pin what an unresolvable participant does.</summary>
    [ExcludeFromDiscovery]
    public sealed class UnresolvableLatePre : ICommandPreInterceptor<PoisonTarget>
    {
        public ValueTask<PoisonTarget> HandleAsync(PoisonTarget command, IExecutionContext context)
            => ValueTask.FromResult(command);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_registration_after_a_generated_pipelines_first_dispatch_is_not_observed()
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

        // The pipeline froze at the first dispatch: the interceptor is in the registry and
        // resolvable from the container, and still does not run.
        var after = new PipelineRecorder();
        await mediator.SendAsync(new GeneratedTarget(), after.Commands());
        after.AssertStages("handler");
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_registration_after_a_runtime_pipelines_first_dispatch_is_not_observed()
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
        after.AssertStages("handler");
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_registration_before_the_first_dispatch_joins_the_pipeline()
    {
        await using var provider = new ServiceCollection()
            .AddTransient<EarlyPre>()
            .AddErgosfare(options => options
                .AddCommandModule(commands => commands.Register<EarlyTargetHandler>()))
            .BuildServiceProvider();

        // The freeze happens at the first dispatch, not at container build: a registration
        // landing between the two still joins the pipeline.
        provider.GetRequiredService<IMessageRegistry>().Register(typeof(EarlyPre));

        var recorder = new PipelineRecorder();
        await provider.GetRequiredService<ICommandMediator>().SendAsync(new EarlyTarget(), recorder.Commands());

        recorder.AssertStages("early", "handler");
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_participant_the_container_cannot_resolve_fails_the_first_dispatch()
    {
        await using var provider = new ServiceCollection()
            .AddErgosfare(options => options
                .AddCommandModule(commands => commands.Register<PoisonTargetHandler>()))
            .BuildServiceProvider();

        // The registry accepts the type, but the container was built without it: the
        // pipeline that includes it cannot be constructed, and the first dispatch is
        // where that is discovered. See the README.
        provider.GetRequiredService<IMessageRegistry>().Register(typeof(UnresolvableLatePre));

        var mediator = provider.GetRequiredService<ICommandMediator>();

        var thrown = await Assert.ThrowsAsync<UnresolvableParticipantException>(
            async () => await mediator.SendAsync(new PoisonTarget()));

        Assert.Contains(nameof(UnresolvableLatePre), thrown.Message, StringComparison.Ordinal);
        Assert.Equal(typeof(PoisonTarget), thrown.MessageType);
        Assert.Equal(typeof(UnresolvableLatePre), thrown.ParticipantType);
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
