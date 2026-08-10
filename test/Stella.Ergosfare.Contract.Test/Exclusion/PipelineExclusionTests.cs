using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Contract.Test.Harness;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Generated;

namespace Stella.Ergosfare.Contract.Test.Exclusion;

/// <summary>
/// What <c>[ExcludeFromPipeline]</c> on a message does to an interceptor that reaches it
/// covariantly — registered against a supertype the message implements — and what it
/// deliberately does not do to an interceptor written for the message itself.
/// </summary>
/// <remarks>
/// Each axis declares its own supertype. The registry is process-wide, so a shared
/// supertype would attach both axes' covariant interceptors to both axes' messages, and
/// each container could only resolve its own half.
/// <para>
/// These types stay keyed: the generator skips messages carrying the attribute rather than
/// modeling the exclusion, so no staged plan can serve them and both axes reach the
/// reflective pipeline shape.
/// </para>
/// </remarks>
public abstract class PipelineExclusionContract
{
    /// <summary>The discovery key this area registers under.</summary>
    protected const string Key = "contract.exclude";

    /// <summary>Marks the handler of whichever message was dispatched.</summary>
    [ExcludeFromDiscovery]
    public abstract class AuditedHandlerBase<TCommand> : ICommandHandler<TCommand>
        where TCommand : class, ICommand
    {
        /// <inheritdoc />
        public ValueTask HandleAsync(TCommand command, IExecutionContext context)
        {
            context.Mark("handler");
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>Registered against a supertype: it reaches its messages covariantly.</summary>
    [ExcludeFromDiscovery]
    public abstract class CovariantPreBase<TSupertype> : ICommandPreInterceptor<TSupertype>
        where TSupertype : class, ICommand
    {
        /// <inheritdoc />
        public ValueTask<TSupertype> HandleAsync(TSupertype command, IExecutionContext context)
        {
            context.Mark("pre:covariant");
            return ValueTask.FromResult(command);
        }
    }

    /// <summary>Registered against the excluded message itself, deliberately.</summary>
    [ExcludeFromDiscovery]
    public abstract class DirectPreBase<TCommand> : ICommandPreInterceptor<TCommand>
        where TCommand : class, ICommand
    {
        /// <inheritdoc />
        public ValueTask<TCommand> HandleAsync(TCommand command, IExecutionContext context)
        {
            context.Mark("pre:direct");
            return ValueTask.FromResult(command);
        }
    }

    /// <summary>A container with this axis' exclusion types registered its own way.</summary>
    protected abstract ServiceProvider CreateProvider();

    /// <summary>The message carrying <c>[ExcludeFromPipeline]</c>.</summary>
    protected abstract ICommand NewExcludedCommand();

    /// <summary>Its sibling, implementing the same supertype without the attribute.</summary>
    protected abstract ICommand NewIncludedCommand();

    private PipelineRecorder NewRecorder() => new() { Label = GetType().Name };

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_covariantly_matched_interceptor_stays_out_of_a_message_marked_ExcludeFromPipeline()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();

        await provider.GetRequiredService<ICommandMediator>()
            .SendAsync(NewExcludedCommand(), recorder.Commands());

        Assert.DoesNotContain("pre:covariant", recorder.Stages);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task An_interceptor_registered_against_the_message_itself_still_runs_on_it()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();

        await provider.GetRequiredService<ICommandMediator>()
            .SendAsync(NewExcludedCommand(), recorder.Commands());

        // The attribute suppresses covariant matching only; an interceptor written for this
        // message was written for it deliberately.
        recorder.AssertStages("pre:direct", "handler");
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task The_same_covariant_interceptor_still_joins_a_sibling_without_the_attribute()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();

        await provider.GetRequiredService<ICommandMediator>()
            .SendAsync(NewIncludedCommand(), recorder.Commands());

        recorder.AssertStages("pre:covariant", "handler");
    }
}

/// <summary>The exclusion types registered through generated descriptors.</summary>
public static class KeyedExclusionTypes
{
    /// <summary>The axis-local supertype the covariant interceptor is registered against.</summary>
    [ExcludeFromDiscovery]
    public interface IAudited : ICommand;

    /// <summary>The message carrying [ExcludeFromPipeline].</summary>
    [DiscoveryKey("contract.exclude")]
    [ExcludeFromPipeline]
    public sealed class ExcludedCommand : IAudited;

    /// <inheritdoc />
    [DiscoveryKey("contract.exclude")]
    public sealed class ExcludedCommandHandler : PipelineExclusionContract.AuditedHandlerBase<ExcludedCommand>;

    /// <inheritdoc />
    [DiscoveryKey("contract.exclude")]
    public sealed class ExcludedCommandDirectPre : PipelineExclusionContract.DirectPreBase<ExcludedCommand>;

    /// <summary>Its sibling, same supertype, without the attribute.</summary>
    [DiscoveryKey("contract.exclude")]
    public sealed class IncludedCommand : IAudited;

    /// <inheritdoc />
    [DiscoveryKey("contract.exclude")]
    public sealed class IncludedCommandHandler : PipelineExclusionContract.AuditedHandlerBase<IncludedCommand>;

    /// <inheritdoc />
    [DiscoveryKey("contract.exclude")]
    public sealed class AuditedPre : PipelineExclusionContract.CovariantPreBase<IAudited>;
}

/// <summary>The same shapes, hidden from the generator so <c>Register&lt;T&gt;()</c> is reflective.</summary>
public static class FallbackExclusionTypes
{
    /// <inheritdoc cref="KeyedExclusionTypes.IAudited"/>
    [ExcludeFromDiscovery]
    public interface IAudited : ICommand;

    /// <inheritdoc cref="KeyedExclusionTypes.ExcludedCommand"/>
    [ExcludeFromDiscovery]
    [ExcludeFromPipeline]
    public sealed class ExcludedCommand : IAudited;

    /// <inheritdoc />
    [ExcludeFromDiscovery]
    public sealed class ExcludedCommandHandler : PipelineExclusionContract.AuditedHandlerBase<ExcludedCommand>;

    /// <inheritdoc />
    [ExcludeFromDiscovery]
    public sealed class ExcludedCommandDirectPre : PipelineExclusionContract.DirectPreBase<ExcludedCommand>;

    /// <inheritdoc cref="KeyedExclusionTypes.IncludedCommand"/>
    [ExcludeFromDiscovery]
    public sealed class IncludedCommand : IAudited;

    /// <inheritdoc />
    [ExcludeFromDiscovery]
    public sealed class IncludedCommandHandler : PipelineExclusionContract.AuditedHandlerBase<IncludedCommand>;

    /// <inheritdoc />
    [ExcludeFromDiscovery]
    public sealed class AuditedPre : PipelineExclusionContract.CovariantPreBase<IAudited>;
}

/// <summary>The exclusion contract under generated (keyed) registration.</summary>
public sealed class GeneratedRegistrationPipelineExclusionTests : PipelineExclusionContract
{
    /// <inheritdoc />
    protected override ServiceProvider CreateProvider()
        => new ServiceCollection()
            .AddErgosfare(options => options
                .AddCommandModule(commands => commands.RegisterGenerated(Key)))
            .BuildServiceProvider();

    /// <inheritdoc />
    protected override ICommand NewExcludedCommand() => new KeyedExclusionTypes.ExcludedCommand();

    /// <inheritdoc />
    protected override ICommand NewIncludedCommand() => new KeyedExclusionTypes.IncludedCommand();
}

/// <summary>The same contract under explicit runtime registration.</summary>
public sealed class RuntimeRegistrationPipelineExclusionTests : PipelineExclusionContract
{
    /// <inheritdoc />
    protected override ServiceProvider CreateProvider()
        => new ServiceCollection()
            .AddErgosfare(options => options
                .AddCommandModule(commands => commands
                    .Register<FallbackExclusionTypes.ExcludedCommandHandler>()
                    .Register<FallbackExclusionTypes.ExcludedCommandDirectPre>()
                    .Register<FallbackExclusionTypes.IncludedCommandHandler>()
                    .Register<FallbackExclusionTypes.AuditedPre>()))
            .BuildServiceProvider();

    /// <inheritdoc />
    protected override ICommand NewExcludedCommand() => new FallbackExclusionTypes.ExcludedCommand();

    /// <inheritdoc />
    protected override ICommand NewIncludedCommand() => new FallbackExclusionTypes.IncludedCommand();
}
