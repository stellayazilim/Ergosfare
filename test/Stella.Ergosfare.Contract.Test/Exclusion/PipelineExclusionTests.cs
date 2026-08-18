using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Contract.Test.Harness;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

namespace Stella.Ergosfare.Contract.Test.Exclusion;

// Top-level and unkeyed so the generator sees everything here; what it does and does not
// bake for these messages is the contract being pinned. The supertype stays area-local —
// a marker-wide interceptor would attach to every pipeline in the process.

/// <summary>The area-local supertype the covariant interceptor is registered against.</summary>
[ExcludeFromDiscovery]
public interface IAuditedForExclusion : ICommand;

/// <summary>An excluded message whose pipeline would need a staged plan.</summary>
[ExcludeFromPipeline]
public sealed class ExcludedCommand : IAuditedForExclusion;

/// <inheritdoc cref="ExcludedCommand"/>
public sealed class ExcludedCommandHandler : ICommandHandler<ExcludedCommand>
{
    /// <inheritdoc />
    public ValueTask HandleAsync(ExcludedCommand command, ErgosfareContext context)
    {
        context.Mark("handler");
        return ValueTask.CompletedTask;
    }
}

/// <summary>Registered against the excluded message itself, deliberately.</summary>
public sealed class ExcludedCommandDirectPre : ICommandPreInterceptor<ExcludedCommand>
{
    /// <inheritdoc />
    public ValueTask<ExcludedCommand> HandleAsync(ExcludedCommand command, ErgosfareContext context)
    {
        context.Mark("pre:direct");
        return ValueTask.FromResult(command);
    }
}

/// <summary>An excluded message whose pipeline is a bare handler.</summary>
[ExcludeFromPipeline]
public sealed class ExcludedBareCommand : IAuditedForExclusion;

/// <inheritdoc cref="ExcludedBareCommand"/>
public sealed class ExcludedBareCommandHandler : ICommandHandler<ExcludedBareCommand>
{
    /// <inheritdoc />
    public ValueTask HandleAsync(ExcludedBareCommand command, ErgosfareContext context)
    {
        context.Mark("handler");
        return ValueTask.CompletedTask;
    }
}

/// <summary>A sibling on the same supertype, without the attribute.</summary>
public sealed class IncludedCommand : IAuditedForExclusion;

/// <inheritdoc cref="IncludedCommand"/>
public sealed class IncludedCommandHandler : ICommandHandler<IncludedCommand>
{
    /// <inheritdoc />
    public ValueTask HandleAsync(IncludedCommand command, ErgosfareContext context)
    {
        context.Mark("handler");
        return ValueTask.CompletedTask;
    }
}

/// <summary>Registered against the supertype: it reaches its messages covariantly.</summary>
public sealed class AuditedExclusionPre : ICommandPreInterceptor<IAuditedForExclusion>
{
    /// <inheritdoc />
    public ValueTask<IAuditedForExclusion> HandleAsync(IAuditedForExclusion command, ErgosfareContext context)
    {
        context.Mark("pre:covariant");
        return ValueTask.FromResult(command);
    }
}

/// <summary>
/// What <c>[ExcludeFromPipeline]</c> is on the plan lane. The exclusion survives only in
/// its degenerate case: a bare-handler excluded message gets a single-handler plan, and
/// the covariant interceptor its supertype would attract stays out of it. An excluded
/// message that would need a staged plan — one with a direct interceptor of its own — gets
/// no plan at all: the generator skips the message rather than modeling the exclusion, so
/// the "direct interceptors still run" half of the feature awaits a compiled-plan
/// formulation, and until then such a dispatch fails loudly.
/// </summary>
public sealed class PipelineExclusionTests
{
    private static ServiceProvider CreateProvider()
        => new ServiceCollection()
            .AddErgosfare(options => options
                .AddCommandModule(commands => commands
                    .Register<ExcludedCommandHandler>()
                    .Register<ExcludedCommandDirectPre>()
                    .Register<ExcludedBareCommandHandler>()
                    .Register<IncludedCommandHandler>()
                    .Register<AuditedExclusionPre>()))
            .BuildServiceProvider();

    [Fact]
    [Trait("Category", "Contract")]
    public async Task An_excluded_message_with_a_direct_interceptor_fails_unplanned()
    {
        await using var provider = CreateProvider();
        var recorder = new PipelineRecorder();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        // The generator skips [ExcludeFromPipeline] messages that would need a staged
        // plan; nothing runs, not even the interceptor written for the message itself.
        var thrown = await Assert.ThrowsAsync<UnplannedDispatchException>(
            async () => await mediator.SendAsync(new ExcludedCommand(), recorder.Commands()));

        Assert.Equal(UnplannedDispatchReason.NoCompiledPlan, thrown.Reason);
        Assert.Empty(recorder.Stages);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_bare_handler_excluded_message_still_dispatches_and_keeps_the_covariant_interceptor_out()
    {
        await using var provider = CreateProvider();
        var recorder = new PipelineRecorder();

        // The degenerate case is planned: with no direct interceptors the exclusion leaves
        // a bare handler, and the single-handler plan runs it. Were the attribute ignored,
        // the covariant pre-interceptor would be in the live pipeline and the bare plan
        // could not serve it — so this passing is the exclusion working.
        await provider.GetRequiredService<ICommandMediator>()
            .SendAsync(new ExcludedBareCommand(), recorder.Commands());

        recorder.AssertStages("handler");
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task The_same_covariant_interceptor_still_joins_a_sibling_without_the_attribute()
    {
        await using var provider = CreateProvider();
        var recorder = new PipelineRecorder();

        await provider.GetRequiredService<ICommandMediator>()
            .SendAsync(new IncludedCommand(), recorder.Commands());

        recorder.AssertStages("pre:covariant", "handler");
    }
}
