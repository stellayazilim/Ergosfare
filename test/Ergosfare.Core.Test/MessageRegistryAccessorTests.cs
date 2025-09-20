using Ergosfare.Core.Internal.Registry;

namespace Ergosfare.Core.Test;

/// <summary>
/// Contains unit tests for <see cref="MessageRegistryAccessor"/> ensuring singleton behavior.
/// </summary>
public class MessageRegistryAccessorTests
{
    /// <summary>
    /// Tests that <see cref="MessageRegistryAccessor.Instance"/> always returns the same singleton instance.
    /// </summary>
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