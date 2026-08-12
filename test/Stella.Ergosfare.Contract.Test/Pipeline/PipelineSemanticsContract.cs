using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Contract.Test.Harness;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Queries.Abstractions;

namespace Stella.Ergosfare.Contract.Test.Pipeline;

/// <summary>
/// The pipeline semantics every registration axis must reproduce: what runs, in what
/// order, and what each stage is handed. Both the source-generated axis and the runtime
/// fallback axis inherit these scenarios verbatim, so a divergence between the two shows
/// up as one subclass failing.
/// </summary>
public abstract class PipelineSemanticsContract
{
    /// <summary>A container with this axis' pipeline types registered its own way.</summary>
    protected abstract ServiceProvider CreateProvider();

    /// <summary>The void command whose pipeline carries every interceptor stage.</summary>
    protected abstract IPayloadCommand NewCommand(string payload);

    /// <summary>The void command whose pipeline is a bare handler.</summary>
    protected abstract IPayloadCommand NewBareCommand(string payload);

    /// <summary>The string-result command whose pipeline carries every stage.</summary>
    protected abstract IPayloadResultCommand NewResultCommand(string payload);

    /// <summary>The string-result command whose pipeline is a bare handler.</summary>
    protected abstract IPayloadResultCommand NewBareResultCommand(string payload);

    /// <summary>The value-typed query whose pipeline carries every stage.</summary>
    protected abstract IPayloadValueQuery NewValueQuery(string payload);

    /// <summary>The value-typed query whose pipeline is a bare handler.</summary>
    protected abstract IPayloadValueQuery NewBareValueQuery(string payload);

    /// <summary>The command whose pipeline exists only to expose stage ordering.</summary>
    protected abstract ICommand NewOrderedCommand();

    /// <summary>
    /// A recorder stamped with the running axis, so a diagnostic dump of these shared
    /// scenarios says which registration path produced each trace.
    /// </summary>
    private PipelineRecorder NewRecorder() => new() { Label = GetType().Name };

    // -----------------------------------------------------------------------
    // message and result rewriting
    // -----------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_pre_interceptor_rewrite_is_the_message_the_handler_receives()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();

        await provider.GetRequiredService<ICommandMediator>()
            .SendAsync(NewCommand("ok"), recorder.Commands());

        recorder.AssertStages("pre", "handler", "post", "final");
        Assert.Equal("ok", recorder.DetailOf("pre"));
        Assert.Equal("ok+rewritten", recorder.DetailOf("handler"));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_post_interceptor_rewrite_is_the_result_the_caller_receives()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();

        var result = await provider.GetRequiredService<ICommandMediator>()
            .SendAsync(NewResultCommand("ok"), recorder.Commands());

        recorder.AssertStages("pre", "handler", "post", "final");
        Assert.Equal("ok+rewritten+posted", result);
        Assert.Equal("ok+rewritten|ok+rewritten", recorder.DetailOf("post"));
    }

    // -----------------------------------------------------------------------
    // stage ordering
    // -----------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Contract")]
    public async Task Interceptors_in_a_stage_run_by_descending_weight()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();

        await provider.GetRequiredService<ICommandMediator>()
            .SendAsync(NewOrderedCommand(), recorder.Commands());

        recorder.AssertStages(
            "pre:heavy", "pre:tie-alpha", "pre:tie-omega", "pre:light",
            "handler",
            "post:high", "post:low");
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task Interceptors_of_equal_weight_run_in_ordinal_type_name_order()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();

        await provider.GetRequiredService<ICommandMediator>()
            .SendAsync(NewOrderedCommand(), recorder.Commands());

        Assert.True(
            recorder.IndexOf("pre:tie-alpha") < recorder.IndexOf("pre:tie-omega"),
            recorder.Dump("equal weights must break the tie on the handler type name:"));
    }

    // -----------------------------------------------------------------------
    // exception semantics
    // -----------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_void_handler_exception_is_swallowed_when_an_exception_interceptor_is_registered()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();

        await provider.GetRequiredService<ICommandMediator>()
            .SendAsync(NewCommand("throw"), recorder.Commands());

        recorder.AssertStages("pre", "handler", "exception", "final");
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task An_exception_interceptor_receives_the_rewritten_message_a_null_result_and_the_thrown_exception()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();

        await provider.GetRequiredService<ICommandMediator>()
            .SendAsync(NewCommand("throw"), recorder.Commands());

        Assert.Equal($"throw+rewritten|null|{nameof(PipelineFailure)}", recorder.DetailOf("exception"));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_post_interceptor_does_not_run_when_the_handler_throws()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();

        await provider.GetRequiredService<ICommandMediator>()
            .SendAsync(NewCommand("throw"), recorder.Commands());

        Assert.DoesNotContain("post", recorder.Stages);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_handler_exception_propagates_unwrapped_when_no_exception_interceptor_is_registered()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        var thrown = await Assert.ThrowsAsync<PipelineFailure>(
            async () => await mediator.SendAsync(NewBareCommand("throw"), recorder.Commands()));

        Assert.Equal("void handler failed", thrown.Message);
        recorder.AssertStages("handler");
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_result_handler_exception_yields_the_exception_interceptors_fallback_result()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();

        var result = await provider.GetRequiredService<ICommandMediator>()
            .SendAsync(NewResultCommand("throw"), recorder.Commands());

        recorder.AssertStages("pre", "handler", "exception", "final");
        Assert.Equal(PipelineVocabulary.StringFallback, result);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_result_handler_exception_propagates_unwrapped_without_an_exception_interceptor()
    {
        await using var provider = CreateProvider();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        await Assert.ThrowsAsync<PipelineFailure>(
            async () => await mediator.SendAsync(NewBareResultCommand("throw")));
    }

    // -----------------------------------------------------------------------
    // final semantics
    // -----------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_final_interceptor_runs_on_success_with_no_exception()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();

        await provider.GetRequiredService<ICommandMediator>()
            .SendAsync(NewCommand("ok"), recorder.Commands());

        Assert.Equal($"ok+rewritten|{nameof(Unit)}|none", recorder.DetailOf("final"));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_final_interceptor_runs_after_a_handled_exception_and_receives_it()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();

        await provider.GetRequiredService<ICommandMediator>()
            .SendAsync(NewCommand("throw"), recorder.Commands());

        Assert.Equal($"throw+rewritten|{nameof(Unit)}|{nameof(PipelineFailure)}", recorder.DetailOf("final"));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_final_interceptor_receives_the_post_rewritten_result()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();

        await provider.GetRequiredService<ICommandMediator>()
            .SendAsync(NewResultCommand("ok"), recorder.Commands());

        Assert.Equal("ok+rewritten|ok+rewritten+posted|none", recorder.DetailOf("final"));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_final_interceptor_does_not_run_on_abort()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        await Assert.ThrowsAsync<ExecutionAbortedException>(
            async () => await mediator.SendAsync(NewCommand("abort"), recorder.Commands()));

        // The pipeline was cut at the pre-interceptor; the final stage is downstream of the
        // cut like everything else.
        Assert.DoesNotContain("final", recorder.Stages);
    }

    // -----------------------------------------------------------------------
    // abort semantics
    // -----------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Contract")]
    public async Task Aborting_from_a_pre_interceptor_skips_the_handler_and_reaches_the_caller()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        await Assert.ThrowsAsync<ExecutionAbortedException>(
            async () => await mediator.SendAsync(NewCommand("abort"), recorder.Commands()));

        // Everything downstream of the aborting participant is skipped — the handler, the
        // post stage and the final stage alike.
        recorder.AssertStages("pre");
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task Aborting_before_the_handler_tells_the_caller_rather_than_returning_a_default()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        // The alternative — returning null — is indistinguishable from a handler that
        // legitimately produced nothing, which is exactly why the signal travels instead.
        await Assert.ThrowsAsync<ExecutionAbortedException>(
            async () => await mediator.SendAsync(NewResultCommand("abort"), recorder.Commands()));

        recorder.AssertStages("pre");
    }

    // -----------------------------------------------------------------------
    // value-typed results through the same pipeline
    // -----------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_value_typed_query_flows_through_pre_handler_post_and_final()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();

        var result = await provider.GetRequiredService<IQueryMediator>()
            .QueryAsync(NewValueQuery("ok"), recorder.Queries());

        recorder.AssertStages("pre", "handler", "post", "final");
        Assert.Equal(PipelineVocabulary.HandlerValue + PipelineVocabulary.PostAddend, result);
        Assert.Equal($"ok+rewritten|{PipelineVocabulary.HandlerValue}", recorder.DetailOf("post"));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_value_typed_query_exception_yields_the_interceptors_fallback_value()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();

        var result = await provider.GetRequiredService<IQueryMediator>()
            .QueryAsync(NewValueQuery("throw"), recorder.Queries());

        recorder.AssertStages("pre", "handler", "exception", "final");
        Assert.Equal(PipelineVocabulary.ValueFallback, result);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_value_typed_exception_interceptor_receives_the_result_types_default()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();

        await provider.GetRequiredService<IQueryMediator>()
            .QueryAsync(NewValueQuery("throw"), recorder.Queries());

        Assert.Equal($"throw+rewritten|0|{nameof(PipelineFailure)}", recorder.DetailOf("exception"));
        Assert.Equal($"throw+rewritten|{PipelineVocabulary.ValueFallback}|{nameof(PipelineFailure)}",
            recorder.DetailOf("final"));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_value_typed_query_abort_reaches_the_caller()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();
        var mediator = provider.GetRequiredService<IQueryMediator>();

        await Assert.ThrowsAsync<ExecutionAbortedException>(
            async () => await mediator.QueryAsync(NewValueQuery("abort"), recorder.Queries()));

        recorder.AssertStages("pre");
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_value_typed_handler_exception_propagates_unwrapped_without_an_exception_interceptor()
    {
        await using var provider = CreateProvider();
        var mediator = provider.GetRequiredService<IQueryMediator>();

        await Assert.ThrowsAsync<PipelineFailure>(
            async () => await mediator.QueryAsync(NewBareValueQuery("throw")));
    }

    // -----------------------------------------------------------------------
    // what a resultless pipeline puts in its result slot
    //
    // Appended rather than filed beside the void scenarios above: a member inserted
    // there renumbers the state machines below it and churns the lane map for no reason.
    // -----------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_void_pipelines_result_agnostic_stages_are_handed_the_shared_unit_instance()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();

        await provider.GetRequiredService<ICommandMediator>()
            .SendAsync(NewCommand("ok"), recorder.Commands());

        // Post and final take `object`/`object?` and are handed the one Unit instance —
        // the recorder's renderer compares it by reference, so "Unit" here means that
        // instance and nothing else. The handler produced nothing; this is what "nothing"
        // looks like from a stage's seat.
        Assert.Equal($"ok+rewritten|{nameof(Unit)}", recorder.DetailOf("post"));
        Assert.Equal($"ok+rewritten|{nameof(Unit)}|none", recorder.DetailOf("final"));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_void_pipelines_result_slot_is_empty_until_the_handler_has_run()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();

        await provider.GetRequiredService<ICommandMediator>()
            .SendAsync(NewCommand("throw"), recorder.Commands());

        // Unit is the produced result, not a stand-in for "no result yet": a pipeline that
        // failed inside the handler reaches its exception stage with an empty slot. Being a
        // reference type is what lets that slot stay empty instead of unboxing into a crash.
        Assert.Equal($"throw+rewritten|null|{nameof(PipelineFailure)}", recorder.DetailOf("exception"));
    }

    // -----------------------------------------------------------------------
    // aborting a pipeline that has no interceptor stages
    //
    // Appended for the same reason as the block above.
    // -----------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_handler_aborting_a_pipeline_with_no_interceptors_reaches_the_caller()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        // With nothing to intercept, the executor invokes the handler and never enters the
        // mediation strategy — there is no arm anywhere on this path, and the contract is
        // the same one either way: the signal travels to the caller.
        await Assert.ThrowsAsync<ExecutionAbortedException>(
            async () => await mediator.SendAsync(NewBareCommand("abort"), recorder.Commands()));

        recorder.AssertStages("handler");
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_result_handler_aborting_a_pipeline_with_no_interceptors_reaches_the_caller()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        // The handler abandoned its own result, and says so rather than returning a null
        // the caller would have to interpret.
        await Assert.ThrowsAsync<ExecutionAbortedException>(
            async () => await mediator.SendAsync(NewBareResultCommand("abort"), recorder.Commands()));

        recorder.AssertStages("handler");
    }

    // -----------------------------------------------------------------------
    // async typed interceptors over the Unit slot
    //
    // Appended for the same reason as the blocks above — the factory included: an
    // abstract member inserted among the ones at the top would renumber every state
    // machine below it.
    // -----------------------------------------------------------------------

    /// <summary>The void command whose post, exception and final stages bind async typed over Unit.</summary>
    protected abstract IPayloadCommand NewAsyncTypedCommand(string payload);

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_void_pipelines_async_typed_stages_are_handed_the_shared_unit_instance()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();

        await provider.GetRequiredService<ICommandMediator>()
            .SendAsync(NewAsyncTypedCommand("ok"), recorder.Commands());

        // The third contract shape a resultless stage can take: result-agnostic and sync
        // typed are pinned elsewhere, this is IAsyncXInterceptor<TMessage, Unit>. Same
        // slot, same shared instance, compared by reference in the renderer.
        recorder.AssertStages("handler", "post", "final");
        Assert.Equal($"ok|{nameof(Unit)}", recorder.DetailOf("post"));
        Assert.Equal($"ok|{nameof(Unit)}|none", recorder.DetailOf("final"));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_void_pipelines_async_typed_exception_stage_sees_the_empty_slot()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();

        await provider.GetRequiredService<ICommandMediator>()
            .SendAsync(NewAsyncTypedCommand("throw"), recorder.Commands());

        // The handler failed before producing, so the async typed exception arm is handed
        // an empty slot; the Unit.Value it returns is what the final stage then sees.
        recorder.AssertStages("handler", "exception", "final");
        Assert.Equal($"throw|null|{nameof(PipelineFailure)}", recorder.DetailOf("exception"));
        Assert.Equal($"throw|{nameof(Unit)}|{nameof(PipelineFailure)}", recorder.DetailOf("final"));
    }
}
