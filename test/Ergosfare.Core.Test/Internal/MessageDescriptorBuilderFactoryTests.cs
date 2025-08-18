using Ergosfare.Core.Internal.Factories;

namespace Ergosfare.Core.Test.Internal;

public class MessageDescriptorBuilderFactoryTests
{
    [Fact]
    [Trait("Category", "Coverage")]
    public void MessageDescriptorBuilderFactoryShouldBeDisposable()
    {
        // arrange
        var factory = new MessageDescriptorBuilderFactory();
        
        // act 
        (factory as IDisposable).Dispose();
        var returnedTask = (factory as IAsyncDisposable).DisposeAsync();
        
        // act
        Assert.True(returnedTask.IsCompleted);
    }
}