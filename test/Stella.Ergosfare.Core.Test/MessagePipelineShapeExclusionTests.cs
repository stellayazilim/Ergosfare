using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;
using Stella.Ergosfare.Core.Internal.Mediator;
using Stella.Ergosfare.Core.Internal.Registry.Descriptors;

namespace Stella.Ergosfare.Core.Test;

/// <summary>
/// <c>[ExcludeFromPipeline]</c> semantics in pipeline-shape building: covariantly matched
/// (indirect) interceptors are excluded — blanket or per group — while directly registered
/// interceptors and main handlers (direct and indirect) are never affected.
/// </summary>
public class MessagePipelineShapeExclusionTests
{
    private interface IBaseMessage;

    private sealed record PlainMessage : IBaseMessage;

    [ExcludeFromPipeline]
    private sealed record BlanketExcluded : IBaseMessage;

    [ExcludeFromPipeline("logging")]
    private sealed record LoggingExcluded : IBaseMessage;

    private sealed class BroadPre;

    private sealed class BroadLoggingPre;

    private sealed class ExactPre;

    private sealed class BroadHandler;

    [Fact]
    public void MessageDescriptor_ExposesPipelineExclusionMetadata()
    {
        Assert.False(new MessageDescriptor(typeof(PlainMessage)).ExcludesIndirectInterceptors);
        Assert.Empty(new MessageDescriptor(typeof(PlainMessage)).ExcludedInterceptorGroups);

        Assert.True(new MessageDescriptor(typeof(BlanketExcluded)).ExcludesIndirectInterceptors);
        Assert.Empty(new MessageDescriptor(typeof(BlanketExcluded)).ExcludedInterceptorGroups);

        Assert.False(new MessageDescriptor(typeof(LoggingExcluded)).ExcludesIndirectInterceptors);
        Assert.Equal(["logging"], new MessageDescriptor(typeof(LoggingExcluded)).ExcludedInterceptorGroups);
    }

    [Fact]
    public void BlanketExclusion_DropsIndirectInterceptors_KeepsDirectOnesAndHandlers()
    {
        var descriptor = new MessageDescriptor(typeof(BlanketExcluded));
        descriptor.AddDescriptor(HandlerDescriptors.PreInterceptor(typeof(IBaseMessage), typeof(BroadPre)));
        descriptor.AddDescriptor(HandlerDescriptors.PreInterceptor(typeof(BlanketExcluded), typeof(ExactPre)));
        descriptor.AddDescriptor(HandlerDescriptors.Handler(typeof(IBaseMessage), typeof(object), typeof(BroadHandler)));

        var shape = MessagePipelineShape.Create(typeof(BlanketExcluded), descriptor, []);

        // The covariant pre-interceptor is gone, the exact one stays.
        var pre = Assert.Single(shape.PreInterceptors);
        Assert.Equal(typeof(ExactPre), pre.HandlerType);

        // Indirect main handlers are not the attribute's business.
        var handler = Assert.Single(shape.IndirectHandlers);
        Assert.Equal(typeof(BroadHandler), handler.HandlerType);
    }

    [Fact]
    public void GroupScopedExclusion_DropsOnlyInterceptorsCarryingAnExcludedGroup()
    {
        var descriptor = new MessageDescriptor(typeof(LoggingExcluded));
        descriptor.AddDescriptor(HandlerDescriptors.PreInterceptor(typeof(IBaseMessage), typeof(BroadPre)));
        descriptor.AddDescriptor(HandlerDescriptors.PreInterceptor(
            typeof(IBaseMessage), typeof(BroadLoggingPre), groups: [GroupAttribute.DefaultGroupName, "logging"]));

        var shape = MessagePipelineShape.Create(typeof(LoggingExcluded), descriptor, []);

        var pre = Assert.Single(shape.PreInterceptors);
        Assert.Equal(typeof(BroadPre), pre.HandlerType);
    }

    [Fact]
    public void GroupScopedExclusion_DoesNotTouchDirectInterceptorsInTheExcludedGroup()
    {
        var descriptor = new MessageDescriptor(typeof(LoggingExcluded));
        descriptor.AddDescriptor(HandlerDescriptors.PreInterceptor(
            typeof(LoggingExcluded), typeof(ExactPre), groups: [GroupAttribute.DefaultGroupName, "logging"]));

        var shape = MessagePipelineShape.Create(typeof(LoggingExcluded), descriptor, []);

        var pre = Assert.Single(shape.PreInterceptors);
        Assert.Equal(typeof(ExactPre), pre.HandlerType);
    }

    [Fact]
    public void UnattributedMessage_KeepsItsIndirectInterceptors()
    {
        var descriptor = new MessageDescriptor(typeof(PlainMessage));
        descriptor.AddDescriptor(HandlerDescriptors.PreInterceptor(typeof(IBaseMessage), typeof(BroadPre)));

        var shape = MessagePipelineShape.Create(typeof(PlainMessage), descriptor, []);

        var pre = Assert.Single(shape.PreInterceptors);
        Assert.Equal(typeof(BroadPre), pre.HandlerType);
    }
}
