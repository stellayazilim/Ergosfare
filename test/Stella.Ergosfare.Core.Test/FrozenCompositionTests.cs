using Stella.Ergosfare.Core.Abstractions.DispatchRoots;

namespace Stella.Ergosfare.Core.Test;

/// <summary>
/// What the frozen composition table derives at dispatch: <see cref="FrozenComposition.BuildShape"/>
/// must turn the baked rows into exactly the stage sequences the pipeline runs — baked
/// order preserved, direct before indirect, group filtering with the default-group
/// asymmetry, and open participants closed over the runtime message arguments. Plus the K3
/// miss ladder: a runtime subtype resolves to its nearest frozen ancestor entry, cached per
/// type; a fully foreign type resolves to nothing.
/// </summary>
/// <remarks>
/// These were dual-run parity tests against the registry's shape-builder while both
/// existed. The registry is gone, so the expectations are stated outright — the table is
/// the only authority left, and the sequences below are what the generator bakes for the
/// same pipeline.
/// </remarks>
public class FrozenCompositionTests
{
    private sealed record ParityMessage;

    private sealed class DirectHandler;

    private sealed class BroadHandler;

    // Names chosen so ordinal name order disagrees with weight order where weights differ,
    // and decides where they tie.
    private sealed class AlphaPre;

    private sealed class ZetaPre;

    private sealed class HeavyPre;

    private sealed class BroadPre;

    private sealed class DefaultPost;

    private sealed class ReportingPost;

    /// <summary>
    /// The baked image of a pipeline whose pre-interceptors were declared out of order:
    /// rows arrive sorted per segment (weight descending, then ordinal <c>FullName</c>),
    /// with the covariantly matched participants in their own segments.
    /// </summary>
    private static FrozenComposition BuildParityFrozenComposition()
        => new(
            typeof(ParityMessage),
            handlers: [new FrozenParticipant(typeof(DirectHandler))],
            indirectHandlers: [new FrozenParticipant(typeof(BroadHandler))],
            preInterceptors:
            [
                new FrozenParticipant(typeof(HeavyPre)),
                new FrozenParticipant(typeof(AlphaPre)),
                new FrozenParticipant(typeof(ZetaPre)),
            ],
            indirectPreInterceptors: [new FrozenParticipant(typeof(BroadPre))],
            postInterceptors:
            [
                new FrozenParticipant(typeof(DefaultPost)),
                new FrozenParticipant(typeof(ReportingPost), ["reporting"]),
            ],
            indirectPostInterceptors: [],
            exceptionInterceptors: [],
            indirectExceptionInterceptors: [],
            finalInterceptors: [],
            indirectFinalInterceptors: []);

    [Fact]
    public void DefaultGroupDispatch_RunsTheDefaultGroupRowsInBakedOrder()
    {
        var shape = BuildParityFrozenComposition().BuildShape(typeof(ParityMessage), []);

        Assert.Equal([typeof(DirectHandler)], shape.Handlers);
        Assert.Equal([typeof(BroadHandler)], shape.IndirectHandlers);

        // Direct rows first, in baked order, then the covariant one.
        Assert.Equal(
            [typeof(HeavyPre), typeof(AlphaPre), typeof(ZetaPre), typeof(BroadPre)],
            shape.PreInterceptors);

        // The empty request means the default group: the reporting-only post-interceptor
        // is absent.
        Assert.Equal([typeof(DefaultPost)], shape.PostInterceptors);
    }

    [Fact]
    public void GroupedDispatch_DropsTheDefaultGroupUnlessItIsAskedForToo()
    {
        var composition = BuildParityFrozenComposition();

        // A named group request drops default-group participants — the asymmetry the
        // shape-builder encodes.
        Assert.Equal(
            [typeof(ReportingPost)],
            composition.BuildShape(typeof(ParityMessage), ["reporting"]).PostInterceptors);

        // And a request that lists both groups sees both participants, in baked order.
        Assert.Equal(
            [typeof(DefaultPost), typeof(ReportingPost)],
            composition.BuildShape(typeof(ParityMessage), ["default", "reporting"]).PostInterceptors);
    }

    // The parameter is the scenario: these exist to be generic, not to use T.
    // ReSharper disable once UnusedTypeParameter
    private sealed record GenericMessage<T>;

    // The parameter is the scenario: these exist to be generic, not to use T.
    // ReSharper disable once UnusedTypeParameter
    private sealed class GenericHandler<T>;

    [Fact]
    public void OpenParticipants_CloseOverTheRuntimeMessageArguments()
    {
        var frozen = new FrozenComposition(
            typeof(GenericMessage<>),
            handlers: [new FrozenParticipant(typeof(GenericHandler<>))],
            indirectHandlers: [], preInterceptors: [], indirectPreInterceptors: [],
            postInterceptors: [], indirectPostInterceptors: [],
            exceptionInterceptors: [], indirectExceptionInterceptors: [],
            finalInterceptors: [], indirectFinalInterceptors: []);

        var shape = frozen.BuildShape(typeof(GenericMessage<int>), []);

        Assert.Equal(typeof(GenericHandler<int>), Assert.Single(shape.Handlers));
    }

    // --- the K3 miss ladder -----------------------------------------------------

    public record LadderBase;

    public record LadderDerived : LadderBase;

    public sealed record LadderGrandchild : LadderDerived;

    public sealed record LadderForeign;

    private sealed class LadderHandler;

    [Fact]
    public void MissLadder_ServesARuntimeSubtypeFromTheNearestFrozenAncestor()
    {
        var baseComposition = new FrozenComposition(
            typeof(LadderBase),
            handlers: [new FrozenParticipant(typeof(LadderHandler))],
            indirectHandlers: [], preInterceptors: [], indirectPreInterceptors: [],
            postInterceptors: [], indirectPostInterceptors: [],
            exceptionInterceptors: [], indirectExceptionInterceptors: [],
            finalInterceptors: [], indirectFinalInterceptors: []);

        GeneratedDispatchRoots.AddFrozenComposition(baseComposition);

        // A runtime subtype nobody registered — the EF/Castle proxy shape — resolves to
        // its nearest frozen ancestor, and the resolution is cached per runtime type.
        Assert.Same(baseComposition, GeneratedDispatchRoots.FindFrozenComposition(typeof(LadderDerived)));
        Assert.Same(baseComposition, GeneratedDispatchRoots.FindFrozenComposition(typeof(LadderGrandchild)));
        Assert.Same(baseComposition, GeneratedDispatchRoots.FindFrozenComposition(typeof(LadderDerived)));

        // A type whose whole ancestor chain is foreign resolves to nothing — the caller's
        // no-handler guard, the one deliberately remaining corner.
        Assert.Null(GeneratedDispatchRoots.FindFrozenComposition(typeof(LadderForeign)));
    }

    // The parameter is the scenario: the type exists to be generic, not to use T.
    // ReSharper disable once UnusedTypeParameter
    private sealed record LadderGenericMessage<T>;

    [Fact]
    public void MissLadder_NormalizesGenericRuntimeTypesToTheirDefinitions()
    {
        var composition = new FrozenComposition(
            typeof(LadderGenericMessage<>),
            handlers: [new FrozenParticipant(typeof(LadderHandler))],
            indirectHandlers: [], preInterceptors: [], indirectPreInterceptors: [],
            postInterceptors: [], indirectPostInterceptors: [],
            exceptionInterceptors: [], indirectExceptionInterceptors: [],
            finalInterceptors: [], indirectFinalInterceptors: []);

        GeneratedDispatchRoots.AddFrozenComposition(composition);

        Assert.Same(composition, GeneratedDispatchRoots.FindFrozenComposition(typeof(LadderGenericMessage<string>)));
    }
}
