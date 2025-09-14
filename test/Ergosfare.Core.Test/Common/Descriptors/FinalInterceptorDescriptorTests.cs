using Ergosfare.Core.Abstractions.Registry.Descriptors;
using Ergosfare.Core.Internal.Factories;
using Ergosfare.Core.Internal.Registry.Descriptors;
using Ergosfare.Core.Test.__stubs__;

namespace Ergosfare.Core.Test.Common;

public class FinalInterceptorDescriptorTests
{
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void FinalHandlerDescriptorShouldBuildedByFactory()
    {
        // arrange
        var builder = new HandlerDescriptorBuilderFactory();
        
        // act
        var descriptor = builder
            .BuildDescriptors(typeof(StubNonGenericFinalInterceptor))
            .FirstOrDefault();
        
       Assert.NotNull(descriptor);
       Assert.NotNull(descriptor.HandlerType);
       Assert.NotNull((descriptor as IFinalInterceptorDescriptor)?.ResultType);
    }
}