using Ergosfare.Core.Internal.Factories;

namespace Ergosfare.Core.Test.Internal;


/// <summary>
/// Unit tests for <see cref="HandlerDescriptorBuilderFactory"/>.
/// This class primarily ensures proper disposal behavior for coverage purposes.
/// </summary>
public class HandlerDescriptorBuilderFactoryTests
{
    
    /// <summary>
    /// Verifies that <see cref="HandlerDescriptorBuilderFactory"/> can be disposed
    /// both synchronously (<see cref="IDisposable"/>) and asynchronously (<see cref="IAsyncDisposable"/>).
    /// </summary>
    [Fact]
    [Trait("Category", "Coverage")]
    public void MessageDescriptorBuilderFactoryShouldBeDisposable()
    {
        // Arrange: create a new factory instance
        var factory = new HandlerDescriptorBuilderFactory();
        
        // Act: dispose synchronously
        (factory as IDisposable).Dispose();

        // Act: dispose asynchronously
        var returnedTask = (factory as IAsyncDisposable).DisposeAsync();

        // Assert: async dispose task is already completed
        Assert.True(returnedTask.IsCompleted);
    }
}