using Ergosfare.Core.Abstractions.Handlers;
using Ergosfare.Core.Internal.Builders;
using Ergosfare.Core.Internal.Registry.Descriptors;
using Ergosfare.Test.Fixtures.Stubs.Generic;

namespace Ergosfare.Core.Test.Internal;

/// <summary>
/// Unit tests for <see cref="FinalInterceptorDescriptorBuilder"/> using stubs.
/// Covers CanBuild and Build behavior for types implementing <see cref="IFinalInterceptor{TMessage, TResult}"/>.
/// </summary>
public class FinalInterceptorDescriptorBuilderTests
{
    /// <summary>
    /// Verifies that <see cref="FinalInterceptorDescriptorBuilder.CanBuild"/> returns true
    /// for types implementing <see cref="IFinalInterceptor"/> and false otherwise.
    /// </summary>
    [Fact]
    public void CanBuild_ShouldReturnTrueForIFinalInterceptor()
    {
        var builder = new FinalInterceptorDescriptorBuilder();

        Assert.True(builder.CanBuild(typeof(VoidStubGenericFinalInterceptor<StubGenericMessage<string>>)));
        Assert.False(builder.CanBuild(typeof(string))); // unrelated type
    }

    /// <summary>
    /// Verifies that <see cref="FinalInterceptorDescriptorBuilder.Build"/> returns
    /// a correct descriptor for a stub final interceptor using <see cref="StubGenericMessage{T}"/>.
    /// </summary>
    [Fact]
    public void Build_ShouldReturnDescriptorForStubInterceptor()
    {
        var builder = new FinalInterceptorDescriptorBuilder();

        var descriptors = builder.Build(typeof(VoidStubGenericFinalInterceptor<StubGenericMessage<string>>)).ToList();

        Assert.Single(descriptors);
        var desc = descriptors.First() as FinalInterceptorDescriptor;

        Assert.NotNull(desc);
        Assert.Equal(typeof(VoidStubGenericFinalInterceptor<StubGenericMessage<string>>), desc.HandlerType);
        Assert.Equal(typeof(StubGenericMessage<>), desc.MessageType);
        Assert.Equal(typeof(Task), desc.ResultType);
        // Default weight and groups since no attributes applied
        Assert.Equal(0, (int)desc.Weight);
        Assert.Equal(["default"],desc.Groups);
    }

    /// <summary>
    /// Verifies that <see cref="FinalInterceptorDescriptorBuilder.Build"/> correctly handles
    /// generic message types with <see cref="StubGenericMessage{T}"/>.
    /// </summary>
    [Fact]
    public void Build_ShouldReturnGenericMessageType()
    {
        var builder = new FinalInterceptorDescriptorBuilder();

        var descriptors = builder.Build(typeof(VoidStubGenericFinalInterceptor<StubGenericMessage<int>>)).ToList();
        var desc = descriptors.First() as FinalInterceptorDescriptor;

        Assert.NotNull(desc);
        Assert.Equal(typeof(StubGenericMessage<>), desc.MessageType);
        Assert.Equal(typeof(Task), desc.ResultType);
    }
}