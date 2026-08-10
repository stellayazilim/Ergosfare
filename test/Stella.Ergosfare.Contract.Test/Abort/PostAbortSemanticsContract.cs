using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Contract.Test.Harness;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Queries.Abstractions;

namespace Stella.Ergosfare.Contract.Test.Abort;

/// <summary>
/// What aborting from a <em>post</em> interceptor does — the arm where the pipeline has
/// already produced a result. The pre-interceptor abort pinned in
/// <c>PipelineSemanticsContract</c> covers the other arm, where nothing was produced yet;
/// the two disagree about what the final stage is handed, which is the whole point of
/// pinning this one before the abort semantics change.
/// </summary>
/// <remarks>
/// Both registration axes inherit these scenarios verbatim: the generated axis aborts
/// inside the emitted staged-plan body, the runtime axis inside the reflective mediation
/// strategy. Those are two separate implementations of the same catch-when/finally shell,
/// and a divergence shows up as one subclass failing.
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
    public async Task Aborting_from_a_post_interceptor_completes_the_dispatch_without_an_exception()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        await mediator.SendAsync(NewCommand(), recorder.Commands());

        recorder.AssertStages("handler", "post:abort", "final");
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_void_pipelines_final_interceptor_is_handed_the_unit_result_after_a_post_abort()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        await mediator.SendAsync(NewCommand(), recorder.Commands());

        // A pre-interceptor abort leaves this null; by the post stage the handler has run,
        // so the pipeline's one result value is already in the slot and survives.
        Assert.Equal($"{nameof(Unit)}|none", recorder.DetailOf("final"));
    }

    // -----------------------------------------------------------------------
    // string-result pipeline
    // -----------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Contract")]
    public async Task Aborting_from_a_result_pipelines_post_interceptor_delivers_the_produced_result()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        var result = await mediator.SendAsync(NewResultCommand(), recorder.Commands());

        recorder.AssertStages("handler", "post:abort", "final");
        Assert.Equal(AbortVocabulary.HandlerResult, recorder.DetailOf("post:abort"));

        // The handler's result, not the value the aborting interceptor was about to
        // return: the abort unwinds before the post stage writes its result back.
        Assert.Equal(AbortVocabulary.HandlerResult, result);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_result_pipelines_final_interceptor_is_handed_the_handlers_result_after_a_post_abort()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        await mediator.SendAsync(NewResultCommand(), recorder.Commands());

        // The same value the caller gets, and no exception: an abort is not a failure.
        Assert.Equal($"{AbortVocabulary.HandlerResult}|none", recorder.DetailOf("final"));
    }

    // -----------------------------------------------------------------------
    // value-typed pipeline
    // -----------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Contract")]
    public async Task Aborting_from_a_value_typed_querys_post_interceptor_delivers_the_produced_value()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();
        var mediator = provider.GetRequiredService<IQueryMediator>();

        var result = await mediator.QueryAsync(NewValueQuery(), recorder.Queries());

        recorder.AssertStages("handler", "post:abort", "final");

        // A value-typed result is produced just as much as a reference-typed one, so the
        // caller gets the handler's number rather than the type's default.
        Assert.Equal(AbortVocabulary.HandlerValue, result);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_value_typed_querys_final_interceptor_is_handed_the_handlers_value_after_a_post_abort()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();
        var mediator = provider.GetRequiredService<IQueryMediator>();

        await mediator.QueryAsync(NewValueQuery(), recorder.Queries());

        Assert.Equal($"{AbortVocabulary.HandlerValue}|none", recorder.DetailOf("final"));
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

        await mediator.SendAsync(NewResultCommand(), recorder.Commands());

        // An abort is not a failure: the exception stage never sees it, and the lighter
        // post slot never runs.
        Assert.DoesNotContain("post:after", recorder.Stages);
        Assert.DoesNotContain("exception", recorder.Stages);
    }
}
