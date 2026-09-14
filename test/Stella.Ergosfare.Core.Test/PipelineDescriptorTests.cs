using Stella.Ergosfare.Core.Abstractions.Planning;
namespace Stella.Ergosfare.Core.Test;
public class PipelineDescriptorTests
{
    public record LadderBase;

    public record LadderDerived : LadderBase;

    public sealed record LadderGrandchild : LadderDerived;

    public sealed record LadderForeign;

    private sealed class LadderHandler;

    [Fact]
    public void MissLadder_ServesARuntimeSubtypeFromTheNearestFrozenAncestor()
    {
        var baseComposition = new PipelineDescriptor(
            typeof(LadderBase),
            handlers: [new FrozenParticipant(typeof(LadderHandler))],
            indirectHandlers: [], preInterceptors: [], indirectPreInterceptors: [],
            postInterceptors: [], indirectPostInterceptors: [],
            exceptionInterceptors: [], indirectExceptionInterceptors: [],
            finalInterceptors: [], indirectFinalInterceptors: []);

        GeneratedPlanRegistry.AddPipelineDescriptor(baseComposition);

        // A runtime subtype nobody registered — the EF/Castle proxy shape — resolves to
        // its nearest frozen ancestor, and the resolution is cached per runtime type.
        Assert.Same(baseComposition, GeneratedPlanRegistry.FindPipelineDescriptor(typeof(LadderDerived)));
        Assert.Same(baseComposition, GeneratedPlanRegistry.FindPipelineDescriptor(typeof(LadderGrandchild)));
        Assert.Same(baseComposition, GeneratedPlanRegistry.FindPipelineDescriptor(typeof(LadderDerived)));

        // A type whose whole ancestor chain is foreign resolves to nothing — the caller's
        // no-handler guard, the one deliberately remaining corner.
        Assert.Null(GeneratedPlanRegistry.FindPipelineDescriptor(typeof(LadderForeign)));
    }

    // The parameter is the scenario: the type exists to be generic, not to use T.
    // ReSharper disable once UnusedTypeParameter
    private sealed record LadderGenericMessage<T>;

    [Fact]
    public void MissLadder_NormalizesGenericRuntimeTypesToTheirDefinitions()
    {
        var composition = new PipelineDescriptor(
            typeof(LadderGenericMessage<>),
            handlers: [new FrozenParticipant(typeof(LadderHandler))],
            indirectHandlers: [], preInterceptors: [], indirectPreInterceptors: [],
            postInterceptors: [], indirectPostInterceptors: [],
            exceptionInterceptors: [], indirectExceptionInterceptors: [],
            finalInterceptors: [], indirectFinalInterceptors: []);

        GeneratedPlanRegistry.AddPipelineDescriptor(composition);

        Assert.Same(composition, GeneratedPlanRegistry.FindPipelineDescriptor(typeof(LadderGenericMessage<string>)));
    }
}
