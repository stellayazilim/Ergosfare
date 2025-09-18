using Ergosfare.Core.Abstractions.Registry.Descriptors;
using Ergosfare.Test.Fixtures;
using Ergosfare.Test.Fixtures.Stubs.Basic;

namespace Ergosfare.Core.Test.Common;


/// <summary>
/// Unit tests that verify descriptor implementations 
/// consistently expose their <see cref="IHandlerDescriptor"/> metadata.
/// </summary>
public class DescriptorImplementationTests
{
    private static readonly DescriptorFixture Fixture = new();
    
        
    /// <summary>
    /// Provides a set of descriptors for parameterized testing.
    /// </summary>
    public static TheoryData<IHandlerDescriptor> DescriptorTestData =>
    [
        Fixture.CreateMainDescriptor<StubMessage, object, StubVoidHandler>(),
        Fixture.CreatePreDescriptor<StubMessage, StubVoidHandler>(),
        Fixture.CreatePostDescriptor<StubMessage, object, StubVoidHandler>(),
        Fixture.CreateExceptionDescriptor<StubMessage, object, StubVoidHandler>(),
        Fixture.CreateFinalDescriptor<StubMessage, object, StubVoidHandler>()
    ];
    
    
    /// <summary>
    /// Ensures that all descriptors correctly expose 
    /// their <see cref="IHandlerDescriptor.MessageType"/>,
    /// <see cref="IHandlerDescriptor.HandlerType"/>,
    /// and (if applicable) <see cref="IHasResultType.ResultType"/>.
    /// </summary>
    /// <param name="descriptor">The descriptor instance under test.</param>
    [Theory]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    [MemberData(nameof(DescriptorTestData))]
   
    public void DescriptorShouldHaveResultType(IHandlerDescriptor descriptor)
    {
        
        // assert message type
        Assert.Equal(typeof(StubMessage), descriptor.MessageType);
        
        // assert handler type
        Assert.Equal(typeof(StubVoidHandler), descriptor.HandlerType);
        
        // If it supports result types, the ResultType should be typeof(object)
        if (descriptor is IHasResultType hasResultType) Assert.Equal(typeof(object),hasResultType.ResultType);
    
    }

}