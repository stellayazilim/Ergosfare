using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Contract.Test.Harness;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Queries.Abstractions;

namespace Stella.Ergosfare.Contract.Test.Abort;

/// <summary>
/// What aborting from a <em>post</em> interceptor does — the arm where the pipeline has
/// already produced a result. It stops like any other abort: the produced result goes
/// nowhere, because a stopped pipeline has no result to deliver, and the caller is told by
/// the signal itself.
/// </summary>
/// <remarks>
/// Both registration axes inherit these scenarios verbatim: the generated axis aborts
/// inside the emitted staged-plan body, the runtime axis inside the reflective mediation
/// strategy. Those are two separate implementations of the same shell, and a divergence
/// shows up as one subclass failing.
/// </remarks>
public abstract class PostAbortSemanticsContract
{
    /// <summary>A container with this axis' post-abort types registered its own way.</summary>
    protected abstract ServiceProvider CreateProvider();

    /// <summary>The void command whose post stage aborts.</summary>
    protected abstract IAbortCommand NewCommand();

    /// <summary>The string-result command whose post stage aborts.</summary>
    protected abstract IAbortResultCommand NewResultCommand();

    /// <summary>The value-typed query whose post stage aborts.</summary>
    protected abstract IAbortValueQuery NewValueQuery();

    /// <summary>
    /// A recorder stamped with the running axis, so a diagnostic dump of these shared
    /// scenarios says which registration path produced each trace.
    /// </summary>
    private PipelineRecorder NewRecorder() => new() { Label = GetType().Name };

    // -----------------------------------------------------------------------
    // void pipeline
    // -----------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Contract")]
    public async Task Aborting_from_a_post_interceptor_reaches_the_caller()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        await Assert.ThrowsAsync<ExecutionAbortedException>(
            async () => await mediator.SendAsync(NewCommand(), recorder.Commands()));

        recorder.AssertStages("handler", "post:abort");
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_void_pipelines_final_interceptor_does_not_run_after_a_post_abort()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        await Assert.ThrowsAsync<ExecutionAbortedException>(
            async () => await mediator.SendAsync(NewCommand(), recorder.Commands()));

        // Stopping means stopping: the handler having run does not buy the final stage a
        // turn, because the pipeline was cut before it.
        Assert.DoesNotContain("final", recorder.Stages);
    }

    // -----------------------------------------------------------------------
    // string-result pipeline
    // -----------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Contract")]
    public async Task Aborting_from_a_result_pipelines_post_interceptor_delivers_no_result()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        // The handler produced a result and the post stage saw it, but the abort ends the
        // dispatch: there is no return value at all, only the signal.
        await Assert.ThrowsAsync<ExecutionAbortedException>(
            async () => await mediator.SendAsync(NewResultCommand(), recorder.Commands()));

        recorder.AssertStages("handler", "post:abort");
        Assert.Equal(AbortVocabulary.HandlerResult, recorder.DetailOf("post:abort"));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_result_pipelines_final_interceptor_does_not_run_after_a_post_abort()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        await Assert.ThrowsAsync<ExecutionAbortedException>(
            async () => await mediator.SendAsync(NewResultCommand(), recorder.Commands()));

        Assert.DoesNotContain("final", recorder.Stages);
    }

    // -----------------------------------------------------------------------
    // value-typed pipeline
    // -----------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Contract")]
    public async Task Aborting_from_a_value_typed_querys_post_interceptor_reaches_the_caller()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();
        var mediator = provider.GetRequiredService<IQueryMediator>();

        // A value-typed pipeline has no "default" to hide behind either: the caller gets
        // the signal, not a zero it would have to interpret.
        await Assert.ThrowsAsync<ExecutionAbortedException>(
            async () => await mediator.QueryAsync(NewValueQuery(), recorder.Queries()));

        recorder.AssertStages("handler", "post:abort");
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_value_typed_querys_final_interceptor_does_not_run_after_a_post_abort()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();
        var mediator = provider.GetRequiredService<IQueryMediator>();

        await Assert.ThrowsAsync<ExecutionAbortedException>(
            async () => await mediator.QueryAsync(NewValueQuery(), recorder.Queries()));

        Assert.DoesNotContain("final", recorder.Stages);
    }

    // -----------------------------------------------------------------------
    // what the abort stops
    // -----------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_post_abort_skips_the_remaining_post_interceptors_and_the_exception_stage()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        await Assert.ThrowsAsync<ExecutionAbortedException>(
            async () => await mediator.SendAsync(NewResultCommand(), recorder.Commands()));

        // An abort is not a failure: the exception stage never sees it, and the lighter
        // post slot never runs.
        Assert.DoesNotContain("post:after", recorder.Stages);
        Assert.DoesNotContain("exception", recorder.Stages);
    }
}
