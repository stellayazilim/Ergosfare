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
    public void UnknownSubtypes_DoNotAcquireAnAncestorDescriptor()
    {
        var baseComposition = new PipelineDescriptor(
            typeof(LadderBase),
            handlers: [new FrozenParticipant(typeof(LadderHandler))],
            indirectHandlers: [], preInterceptors: [], indirectPreInterceptors: [],
            postInterceptors: [], indirectPostInterceptors: [],
            exceptionInterceptors: [], indirectExceptionInterceptors: [],
            finalInterceptors: [], indirectFinalInterceptors: []);

        GeneratedPlanRegistry.AddPipelineDescriptor(baseComposition);

        // An unknown subtype does not acquire metadata from an ancestor.
        Assert.Null(GeneratedPlanRegistry.FindPipelineDescriptor(typeof(LadderDerived)));
        Assert.Null(GeneratedPlanRegistry.FindPipelineDescriptor(typeof(LadderGrandchild)));
        Assert.Null(GeneratedPlanRegistry.FindPipelineDescriptor(typeof(LadderDerived)));

        // Unknown types are handled by the missing-plan guard.
        Assert.Null(GeneratedPlanRegistry.FindPipelineDescriptor(typeof(LadderForeign)));
    }

    // The parameter is the scenario: the type exists to be generic, not to use T.
    // ReSharper disable once UnusedTypeParameter
    private sealed record LadderGenericMessage<T>;

    [Fact]
    public void UnknownClosedTypes_DoNotAcquireAnOpenGenericDescriptor()
    {
        var composition = new PipelineDescriptor(
            typeof(LadderGenericMessage<>),
            handlers: [new FrozenParticipant(typeof(LadderHandler))],
            indirectHandlers: [], preInterceptors: [], indirectPreInterceptors: [],
            postInterceptors: [], indirectPostInterceptors: [],
            exceptionInterceptors: [], indirectExceptionInterceptors: [],
            finalInterceptors: [], indirectFinalInterceptors: []);

        GeneratedPlanRegistry.AddPipelineDescriptor(composition);

        Assert.Null(GeneratedPlanRegistry.FindPipelineDescriptor(typeof(LadderGenericMessage<string>)));
        Assert.Same(composition, GeneratedPlanRegistry.FindPipelineDescriptor(typeof(LadderGenericMessage<>)));
    }
}
