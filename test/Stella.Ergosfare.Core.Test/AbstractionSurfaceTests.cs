using System.Collections;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Abstractions.DispatchSites;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Abstractions.Results;
using Stella.Ergosfare.Core.Abstractions.StagedPlans;

namespace Stella.Ergosfare.Core.Test;

/// <summary>
/// The parts of the abstractions a consumer reads rather than a pipeline executes: what an
/// exception carries once it reaches a <c>catch</c>, what an attribute records for the
/// generator, and what a plan key exposes about the composition it was baked against.
/// </summary>
/// <remarks>
/// None of this is reached by dispatching, which is why it survived so long uncovered — and
/// why it is worth pinning: an exception's payload and an attribute's recorded values are the
/// public API of the diagnostic paths, and the plan key's is the advisory contract's.
/// </remarks>
public class AbstractionSurfaceTests
{
    private sealed record ProbeMessage;

    private sealed class ProbeHandler;

    private sealed class ProbeInterceptor;

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void Abort_CarriesWhateverTheParticipantChoseToSay()
    {
        // Nothing said: the signal still has to read as something in a log.
        var bare = new ExecutionAbortedException();

        Assert.Equal("Execution was aborted", bare.Reason);
        Assert.Null(bare.Value);

        var reasoned = new ExecutionAbortedException("refused");

        Assert.Equal("refused", reasoned.Reason);
        Assert.Equal(reasoned.Message, reasoned.Reason);
        Assert.Null(reasoned.Value);

        var payload = new object();
        var carried = new ExecutionAbortedException("refused", payload);

        Assert.Equal("refused", carried.Reason);
        Assert.Same(payload, carried.Value);

        // A null reason falls back rather than producing a message-less exception — the
        // participant said nothing, which is not the same as saying nothing happened.
        Assert.Equal("Execution was aborted", new ExecutionAbortedException(null).Reason);
        Assert.Equal("Execution was aborted", new ExecutionAbortedException(null, payload).Reason);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void RetryRequest_CountsTheIterationItWasRaisedOn()
    {
        Assert.Equal(0, new ExecutionRetryRequestedException().Counter);
        Assert.Equal(3, new ExecutionRetryRequestedException(3).Counter);
        Assert.Contains("current iteration 3", new ExecutionRetryRequestedException(3).Message);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void UnresolvableParticipant_NamesBothTheMessageAndTheParticipant()
    {
        var thrown = new UnresolvableParticipantException(typeof(ProbeMessage), typeof(ProbeHandler));

        Assert.Equal(typeof(ProbeMessage), thrown.MessageType);
        Assert.Equal(typeof(ProbeHandler), thrown.ParticipantType);

        // The message has to be actionable on its own: it is read from a container-build
        // failure, where neither type is otherwise visible.
        Assert.Contains(nameof(ProbeMessage), thrown.Message);
        Assert.Contains(typeof(ProbeHandler).FullName!, thrown.Message);
        Assert.IsAssignableFrom<InvalidOperationException>(thrown);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void MultipleHandlerFound_CountsWhatItFound()
    {
        var thrown = new MultipleHandlerFoundException(typeof(ProbeMessage), 2);

        Assert.Equal(typeof(ProbeMessage), thrown.MessageType);
        Assert.Equal(2, thrown.NumberOfHandlers);
        Assert.Contains("2 handlers", thrown.Message);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void AdaptedException_KeepsTheOriginalResultByReference()
    {
        var original = new ProbeMessage();
        var thrown = new AdaptedException("adapted", original);

        Assert.Same(original, thrown.OriginalResult);
        Assert.Same(original, thrown.GetOriginalResult<ProbeMessage>());
        Assert.Equal("adapted", thrown.Message);

        // Asking for the wrong type is a cast, and fails like one.
        Assert.Throws<InvalidCastException>(() => thrown.GetOriginalResult<string>());

        // Carrying nothing would defeat the wrapper's only purpose.
        Assert.Throws<ArgumentNullException>(() => new AdaptedException("adapted", null!));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void Attributes_RecordWhatTheyWereWrittenWith()
    {
        Assert.Equal(7u, new WeightAttribute(7).Weight);
        Assert.Equal(new[] { "reporting", "auditing" },
            new GroupAttribute("reporting", "auditing").GroupNames);

        // No names means every covariantly matched interceptor, so the empty array is the
        // meaningful value here rather than a missing one.
        Assert.Empty(new ExcludeFromPipelineAttribute().Groups);
        Assert.Equal(new[] { "reporting" }, new ExcludeFromPipelineAttribute("reporting").Groups);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void DispatchManifest_RecordsTheEvidenceAComposition_RootJudgesFrom()
    {
        // These are written by generated code and read back from metadata by the next
        // compilation, so the properties are the wire format between two generator runs.
        var manifest = new DispatchManifestAttribute(1);

        Assert.Equal(1, manifest.Version);
        Assert.False(manifest.HasOpaqueRegistrations);

        manifest.HasOpaqueRegistrations = true;

        Assert.True(manifest.HasOpaqueRegistrations);

        var site = new DispatchSiteAttribute("Ns.Probe`1", DispatchKind.Command, opaque: true);

        Assert.Equal("Ns.Probe`1", site.MessageTypeMetadataName);
        Assert.Equal(DispatchKind.Command, site.Kind);
        Assert.True(site.Opaque);
        Assert.Null(site.Groups);

        site.Groups = ["reporting"];

        Assert.Equal(new[] { "reporting" }, site.Groups);

        Assert.Equal("Ns.Probe", new ManualRegistrationAttribute("Ns.Probe").TypeMetadataName);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void PlanKey_WithOneHandler_NamesIt()
    {
        var key = new StagedPlanKey(
            typeof(ProbeHandler),
            [typeof(ProbeInterceptor)],
            [],
            [],
            [],
            typeof(ProbeMessage));

        // "The" handler exists only for a single-handler composition, which is what the
        // executor's advisory comparison keys a command or query plan on.
        Assert.Equal(typeof(ProbeHandler), key.HandlerType);
        Assert.Equal(new[] { typeof(ProbeHandler) }, key.HandlerTypes);
        Assert.Empty(key.IndirectHandlerTypes);
        Assert.Equal(new[] { typeof(ProbeInterceptor) }, key.PreInterceptorTypes);
        Assert.Empty(key.PostInterceptorTypes);
        Assert.Empty(key.ExceptionInterceptorTypes);
        Assert.Empty(key.FinalInterceptorTypes);
        Assert.Equal(typeof(ProbeMessage), key.ResultAdapterType);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void PlanKey_WithSeveralHandlers_HasNoSingleHandler()
    {
        var broadcast = new StagedPlanKey(
            [typeof(ProbeHandler), typeof(ProbeInterceptor)],
            [],
            [],
            [],
            [],
            []);

        Assert.Null(broadcast.HandlerType);
        Assert.Null(broadcast.ResultAdapterType);

        // One direct handler is still not "the" handler once a covariant one joins it: the
        // plans that key on a single handler are disqualified by exactly that.
        var covariant = new StagedPlanKey(
            [typeof(ProbeHandler)],
            [typeof(ProbeInterceptor)],
            [],
            [],
            [],
            []);

        Assert.Null(covariant.HandlerType);
        Assert.Equal(new[] { typeof(ProbeInterceptor) }, covariant.IndirectHandlerTypes);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void GroupSet_ReadsAsItsNames_ThroughEitherEnumerator()
    {
        var set = GroupSet.Of("surface.a", "surface.b");

        Assert.Equal("GroupSet [surface.a, surface.b]", set.ToString());
        Assert.Equal("GroupSet.Empty", GroupSet.Empty.ToString());

        // The non-generic enumerator is what a debugger visualiser and any legacy
        // IEnumerable consumer reach; it must not be a separate, emptier answer.
        var walked = new List<object?>();

        foreach (var name in (IEnumerable) set)
        {
            walked.Add(name);
        }

        Assert.Equal(["surface.a", "surface.b"], walked);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void Results_ReadAsTheirOutcome()
    {
        Assert.Equal("Ok", Result.Ok().ToString());
        Assert.Equal("Fail(InvalidOperationException)", Result.Fail(new InvalidOperationException()).ToString());

        Assert.Equal("Ok(42)", Result<int>.Ok(42).ToString());
        Assert.Equal("Fail(TimeoutException)", Result<int>.Fail(new TimeoutException()).ToString());
    }
}
