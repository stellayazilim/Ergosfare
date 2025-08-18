using Ergosfare.Core.Internal.Registry;

namespace Ergosfare.Core.Test;

public class MessageRegistryAccessorTests
{
    [Fact]
    public void MessageRegistryAccessor_ShouldReturnSingletonInstance()
    {
        // arrange & act
        var first = MessageRegistryAccessor.Instance;
        var second = MessageRegistryAccessor.Instance;

        // assert
        Assert.Same(first, second); // reference equality
    }
}