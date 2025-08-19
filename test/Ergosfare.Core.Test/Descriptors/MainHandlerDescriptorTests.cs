using Ergosfare.Core.Internal.Factories;
using Ergosfare.Core.Internal.Registry.Descriptors;
using Ergosfare.Test.__stubs__;

namespace Ergosfare.Core.Test;

public class MainHandlerDescriptorTests
{

    [Fact]
    public void MainHandlerDescriptorShouldGetResultType()
    {
        // arrange
        var builder = new HandlerDescriptorBuilderFactory();
        
        // act
        var descriptor = builder
            .BuildDescriptors(typeof(StubHandlers.StubNonGenericHandler))
            .First() as MainHandlerDescriptor;
  
        Assert.NotNull(descriptor?.ResultType);
        Assert.Equal(typeof(Task), descriptor.ResultType);
    }
}