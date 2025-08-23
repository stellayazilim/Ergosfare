using Ergosfare.Core.Internal.Factories;
using Ergosfare.Core.Internal.Registry.Descriptors;
using Ergosfare.Core.Test.__stubs__;

namespace Ergosfare.Core.Test.Common;
public class MainHandlerDescriptorTests
{

    [Fact]
    public void MainHandlerDescriptorShouldGetResultType()
    {
        // arrange
        var builder = new HandlerDescriptorBuilderFactory();
        
        // act
        var descriptor = builder
            .BuildDescriptors(typeof(StubNonGenericHandler))
            .First() as MainHandlerDescriptor;
  
        Assert.NotNull(descriptor?.ResultType);
        Assert.Equal(typeof(object), descriptor.ResultType);
    }
}