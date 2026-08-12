using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Contract.Test.Harness;

namespace Stella.Ergosfare.Contract.Test.ExceptionFilters;

/// <summary>
/// What declaring an exception type on an exception interceptor does: the interceptor runs
/// only for exceptions it accepts, receives them already typed, and — when nothing in the
/// stage accepts the thrown exception — the exception leaves the pipeline unwrapped
/// instead of being swallowed by participants that declined it.
/// </summary>
/// <remarks>
/// Both registration axes inherit these scenarios verbatim: the generated axis filters
/// through a compile-time <c>is</c> guard baked into the emitted staged-plan body, the
/// runtime axis through the filter probe the invocation strategy asks each resolved
/// instance. Those are two separate implementations of one rule, and a divergence shows up
/// as one subclass failing.
/// </remarks>
public abstract class ExceptionFilterSemanticsContract
{
    /// <summary>A container with this axis' typed exception-interceptor types registered its own way.</summary>
    protected abstract ServiceProvider CreateProvider();

    /// <summary>The void command whose handler throws the given fault.</summary>
    protected abstract IFilteredVoidCommand NewVoidCommand(FaultKind fault);

    /// <summary>The string-result command whose handler throws the given fault.</summary>
    protected abstract IFilteredResultCommand NewResultCommand(FaultKind fault);

    /// <summary>
    /// A recorder stamped with the running axis, so a diagnostic dump of these shared
    /// scenarios says which registration path produced each trace.
    /// </summary>
    private PipelineRecorder NewRecorder() => new() { Label = GetType().Name };

    // -----------------------------------------------------------------------
    // matching
    // -----------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Contract")]
    public async Task An_exception_interceptor_runs_for_the_exception_type_it_declares()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        await mediator.SendAsync(NewVoidCommand(FaultKind.Tagged), recorder.Commands());

        recorder.AssertStages("handler", "exception:tagged", "final");
        Assert.Equal(nameof(TaggedFaultException), recorder.DetailOf("exception:tagged"));
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task An_exception_interceptor_does_not_run_for_an_exception_type_it_did_not_declare()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        await mediator.SendAsync(NewVoidCommand(FaultKind.Unrelated), recorder.Commands());

        // Both interceptors are in the stage; only the one that accepts this fault runs.
        recorder.AssertStages("handler", "exception:unrelated", "final");
        Assert.DoesNotContain("exception:tagged", recorder.Stages);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_declared_exception_type_matches_its_subtypes_the_way_catch_does()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        await mediator.SendAsync(NewVoidCommand(FaultKind.DerivedTagged), recorder.Commands());

        // Assignability, not type identity: the interceptor declared the base fault.
        recorder.AssertStages("handler", "exception:tagged", "final");
        Assert.Equal(nameof(DerivedTaggedFaultException), recorder.DetailOf("exception:tagged"));
    }

    // -----------------------------------------------------------------------
    // nothing matches
    // -----------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Contract")]
    public async Task An_exception_no_interceptor_declared_leaves_the_pipeline_unwrapped()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        // Two interceptors are registered, so presence alone would have swallowed this.
        // Only a *matching* interceptor counts as having handled the exception.
        var thrown = await Assert.ThrowsAsync<StrayFaultException>(
            () => mediator.SendAsync(NewVoidCommand(FaultKind.Stray), recorder.Commands()).AsTask());

        // Not wrapped in anything: the caller catches the type the handler threw.
        Assert.Equal("stray", thrown.Message);
        recorder.AssertStages("handler", "final");
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task The_final_stage_still_runs_when_no_interceptor_accepted_the_exception()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        await Assert.ThrowsAsync<StrayFaultException>(
            () => mediator.SendAsync(NewVoidCommand(FaultKind.Stray), recorder.Commands()).AsTask());

        // The rethrow leaves through the same shell an empty exception stage does, so the
        // final stage observes the failure on its way out — and neither filtered
        // interceptor ran.
        recorder.AssertStages("handler", "final");
        Assert.Equal(nameof(StrayFaultException), recorder.DetailOf("final"));
    }

    // -----------------------------------------------------------------------
    // filtered beside unfiltered
    // -----------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_filtered_and_an_unfiltered_interceptor_both_run_in_the_pipelines_existing_order()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        var result = await mediator.SendAsync(NewResultCommand(FaultKind.Tagged), recorder.Commands());

        // Filtering does not reorder the stage: the heavier slot still runs first, and the
        // result threads from the filtered interceptor into the unfiltered one.
        recorder.AssertStages("handler", "exception:tagged", "exception:untyped", "final");
        Assert.Equal(
            ExceptionFilterVocabulary.TaggedRecovery + ExceptionFilterVocabulary.UntypedSuffix,
            result);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task An_unfiltered_interceptor_still_accepts_what_the_filtered_one_declined()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        var result = await mediator.SendAsync(NewResultCommand(FaultKind.Stray), recorder.Commands());

        // One unfiltered participant is enough for the stage to have handled the
        // exception — the caller sees no exception at all.
        recorder.AssertStages("handler", "exception:untyped", "final");
        Assert.Equal(ExceptionFilterVocabulary.UntypedSuffix, result);
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task The_typed_exception_reaches_the_interceptor_without_a_cast_in_its_body()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();
        var mediator = provider.GetRequiredService<ICommandMediator>();

        await mediator.SendAsync(NewResultCommand(FaultKind.DerivedTagged), recorder.Commands());

        // The interceptor's parameter is TaggedFaultException; what it recorded is the
        // runtime type it was handed through that parameter.
        recorder.AssertStages("handler", "exception:tagged", "exception:untyped", "final");
        Assert.Equal(nameof(DerivedTaggedFaultException), recorder.DetailOf("exception:tagged"));
    }
}
