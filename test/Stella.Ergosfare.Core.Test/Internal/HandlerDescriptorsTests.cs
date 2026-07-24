using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Abstractions.Registry.Descriptors;

namespace Stella.Ergosfare.Core.Test.Internal;

/// <summary>
/// Unit tests for the <see cref="HandlerDescriptors"/> factory — the construction surface
/// used by source-generated registration code.
/// </summary>
public class HandlerDescriptorsTests
{
    private sealed class SomeMessage;

    private sealed class SomeHandler;

    /// <summary>
    /// Omitted groups fall back to <see cref="GroupAttribute.DefaultGroupName"/>, matching
    /// the reflection path's behavior.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void OmittedGroups_FallBackToTheDefaultGroupName()
    {
        var descriptor = HandlerDescriptors.Handler(typeof(SomeMessage), typeof(ValueTask), typeof(SomeHandler));

        Assert.Equal([GroupAttribute.DefaultGroupName], descriptor.Groups);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void FactoryMethods_RoundTripAllDescriptorProperties()
    {
        string[] groups = ["audit", "ops"];

        var main = HandlerDescriptors.Handler(typeof(SomeMessage), typeof(ValueTask<string>), typeof(SomeHandler), 5, groups);
        Assert.Equal(typeof(SomeMessage), main.MessageType);
        Assert.Equal(typeof(ValueTask<string>), main.ResultType);
        Assert.Equal(typeof(SomeHandler), main.HandlerType);
        Assert.Equal(5u, main.Weight);
        Assert.Same(groups, main.Groups);

        var pre = HandlerDescriptors.PreInterceptor(typeof(SomeMessage), typeof(SomeHandler), 3);
        Assert.IsAssignableFrom<IPreInterceptorDescriptor>(pre);
        Assert.Equal(typeof(SomeMessage), pre.MessageType);
        Assert.Equal(3u, pre.Weight);

        var post = HandlerDescriptors.PostInterceptor(typeof(SomeMessage), typeof(object), typeof(SomeHandler));
        Assert.IsAssignableFrom<IPostInterceptorDescriptor>(post);
        Assert.Equal(typeof(object), post.ResultType);

        var exception = HandlerDescriptors.ExceptionInterceptor(typeof(SomeMessage), typeof(object), typeof(SomeHandler));
        Assert.IsAssignableFrom<IExceptionInterceptorDescriptor>(exception);
        Assert.Equal(typeof(object), exception.ResultType);

        var final = HandlerDescriptors.FinalInterceptor(typeof(SomeMessage), typeof(object), typeof(SomeHandler));
        Assert.IsAssignableFrom<IFinalInterceptorDescriptor>(final);
        Assert.Equal(typeof(object), final.ResultType);
    }
}
