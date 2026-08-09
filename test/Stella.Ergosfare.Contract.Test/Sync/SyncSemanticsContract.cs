using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Contract.Test.Harness;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Exceptions;

namespace Stella.Ergosfare.Contract.Test.Sync;

/// <summary>
/// What the synchronous interceptor contracts do inside a pipeline: which stage each
/// flavor serves, what it is handed, and how it orders against its asynchronous twins.
/// Both registration axes inherit these scenarios verbatim, so a divergence between the
/// emitted synchronous calls and the reflective synchronous arms shows up as one subclass
/// failing.
/// </summary>
/// <remarks>
/// The main handlers here are asynchronous: a synchronous main handler disqualifies its
/// message from every compile-time plan, so it could never exercise the emitted calls.
/// <see cref="SyncMainHandlerContract"/> covers the synchronous main-handler contracts.
/// </remarks>
public abstract class SyncSemanticsContract
{
    /// <summary>A container with this axis' synchronous types registered its own way.</summary>
    protected abstract ServiceProvider CreateProvider();

    /// <summary>The void command whose interceptor stages are all synchronous.</summary>
    protected abstract ISyncPayloadCommand NewCommand(string payload);

    /// <summary>The string-result command whose interceptor stages are all synchronous.</summary>
    protected abstract ISyncPayloadResultCommand NewResultCommand(string payload);

    /// <summary>The command whose stages interleave the two flavors.</summary>
    protected abstract ISyncPayloadCommand NewOrderedCommand();

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
    public async Task A_synchronous_pre_interceptor_rewrite_is_the_message_the_handler_receives()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();

        await provider.GetRequiredService<ICommandMediator>()
            .SendAsync(NewCommand("ok"), recorder.Commands());

        recorder.AssertStages("pre", "handler", "post", "final");
        Assert.Equal("ok", recorder.DetailOf("pre"));
        Assert.Equal("ok" + SyncVocabulary.Rewritten, recorder.DetailOf("handler"));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_synchronous_void_post_interceptor_is_handed_the_pipelines_unit_result()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();

        await provider.GetRequiredService<ICommandMediator>()
            .SendAsync(NewCommand("ok"), recorder.Commands());

        Assert.Equal($"ok{SyncVocabulary.Rewritten}|{nameof(Unit)}", recorder.DetailOf("post"));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_synchronous_void_final_interceptor_sees_the_unit_result_and_no_exception()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();

        await provider.GetRequiredService<ICommandMediator>()
            .SendAsync(NewCommand("ok"), recorder.Commands());

        Assert.Equal($"ok{SyncVocabulary.Rewritten}|{nameof(Unit)}|none", recorder.DetailOf("final"));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_void_pipelines_synchronous_stages_run_with_an_empty_result_when_the_handler_throws()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        // The result slot is a reference type now, so the empty slot of a failed void
        // pipeline reaches the synchronous exception and final contracts as null instead
        // of being unboxed into a ValueTask parameter. Both stages run, and the handler's
        // own exception is the one the pipeline goes on to handle.
        await mediator.SendAsync(NewCommand("throw"), recorder.Commands());

        recorder.AssertStages("pre", "handler", "exception", "final");
        Assert.Equal($"throw{SyncVocabulary.Rewritten}|null|{nameof(SyncFailure)}", recorder.DetailOf("exception"));
        Assert.Equal($"throw{SyncVocabulary.Rewritten}|null|{nameof(SyncFailure)}", recorder.DetailOf("final"));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_void_pipelines_synchronous_final_interceptor_runs_on_abort_with_no_result()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        // Same empty slot, reached through the abort path — the final stage sees it as
        // null and ExecutionAbortedException reaches the caller unreplaced.
        await Assert.ThrowsAsync<ExecutionAbortedException>(
            async () => await mediator.SendAsync(NewCommand("abort"), recorder.Commands()));

        recorder.AssertStages("pre", "final");
        Assert.Equal("abort|null|none", recorder.DetailOf("final"));
    }

    // -----------------------------------------------------------------------
    // string-result pipeline
    // -----------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_synchronous_post_interceptor_rewrite_is_the_result_the_caller_receives()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();

        var result = await provider.GetRequiredService<ICommandMediator>()
            .SendAsync(NewResultCommand("ok"), recorder.Commands());

        recorder.AssertStages("pre", "handler", "post", "final");
        Assert.Equal($"ok{SyncVocabulary.Rewritten}{SyncVocabulary.Posted}", result);
        Assert.Equal($"ok{SyncVocabulary.Rewritten}|ok{SyncVocabulary.Rewritten}", recorder.DetailOf("post"));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_synchronous_final_interceptor_receives_the_post_rewritten_result()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();

        await provider.GetRequiredService<ICommandMediator>()
            .SendAsync(NewResultCommand("ok"), recorder.Commands());

        Assert.Equal(
            $"ok{SyncVocabulary.Rewritten}|ok{SyncVocabulary.Rewritten}{SyncVocabulary.Posted}|none",
            recorder.DetailOf("final"));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_synchronous_exception_interceptor_receives_the_rewritten_message_a_null_result_and_the_thrown_exception()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();

        var result = await provider.GetRequiredService<ICommandMediator>()
            .SendAsync(NewResultCommand("throw"), recorder.Commands());

        recorder.AssertStages("pre", "handler", "exception", "final");
        Assert.Equal($"throw{SyncVocabulary.Rewritten}|null|{nameof(SyncFailure)}", recorder.DetailOf("exception"));
        Assert.Equal(SyncVocabulary.StringFallback, result);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_synchronous_final_interceptor_runs_after_a_handled_exception_and_receives_it()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();

        await provider.GetRequiredService<ICommandMediator>()
            .SendAsync(NewResultCommand("throw"), recorder.Commands());

        Assert.Equal(
            $"throw{SyncVocabulary.Rewritten}|{SyncVocabulary.StringFallback}|{nameof(SyncFailure)}",
            recorder.DetailOf("final"));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_result_pipelines_synchronous_final_interceptor_runs_on_abort_with_no_result()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        await Assert.ThrowsAsync<ExecutionAbortedException>(
            async () => await mediator.SendAsync(NewResultCommand("abort"), recorder.Commands()));

        recorder.AssertStages("pre", "final");
        Assert.Equal("abort|null|none", recorder.DetailOf("final"));
    }

    // -----------------------------------------------------------------------
    // stage ordering across the two flavors
    // -----------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Contract")]
    public async Task Synchronous_and_asynchronous_interceptors_share_one_weight_order()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();

        await provider.GetRequiredService<ICommandMediator>()
            .SendAsync(NewOrderedCommand(), recorder.Commands());

        recorder.AssertStages(
            "pre:async-high", "pre:sync-mid", "pre:async-low",
            "handler",
            "post:sync-high", "post:async-low");
    }

    // -----------------------------------------------------------------------
    // the result key a resultless pipeline matches on
    //
    // Appended rather than filed beside their siblings: a member inserted above
    // renumbers the state machines below it and churns the lane map for no reason.
    // -----------------------------------------------------------------------

    /// <summary>The void command whose only post interceptor uses the pre-Unit result key.</summary>
    protected abstract ISyncPayloadCommand NewStaleKeyCommand();

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_synchronous_void_interceptor_is_matched_on_the_unit_result_key()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();

        await provider.GetRequiredService<ICommandMediator>()
            .SendAsync(NewCommand("ok"), recorder.Commands());

        // Both typed stages are declared as IPostInterceptor<T, Unit> / IFinalInterceptor<T, Unit>.
        // Running at all is the pin: a resultless pipeline closes its stages over Unit, so a
        // contract keyed on anything else is invisible to the pattern match.
        recorder.AssertStages("pre", "handler", "post", "final");
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_void_interceptor_keyed_on_the_old_result_type_fails_the_dispatch_loudly()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        // The deliberate break of the Unit migration: an interceptor still written against
        // the completed-task key registers fine and then matches no arm. The dispatch says
        // so instead of silently skipping the stage.
        await Assert.ThrowsAsync<NotSupportedException>(
            async () => await mediator.SendAsync(NewStaleKeyCommand(), recorder.Commands()));

        Assert.DoesNotContain("post:stale", recorder.Stages);
    }
}
