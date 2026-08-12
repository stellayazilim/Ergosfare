using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Core.Internal.Factories;
using Stella.Ergosfare.Core.Internal.Mediator;

namespace Stella.Ergosfare.Core.Test;


/// <summary>
/// Contains unit tests for <see cref="MessageMediator"/> ensuring proper construction,
/// error handling, and execution context management.
/// </summary>
public class MessageMediatorTests
{
    /// <summary>
    /// Tests that the <see cref="MessageMediator"/> constructor throws <see cref="ArgumentNullException"/>
    /// when required arguments are null.
    /// </summary>
    [Fact]
    [Trait("Category", "Coverage")]
    public void MessageMediatorShouldThrowArgumentNullException()
    {       
        // act & assert
        Assert.Throws<ArgumentNullException>(
            () => new MessageMediator(null!, null!));
        Assert.Throws<ArgumentNullException>(
            () => new MessageMediator(new MessageDependenciesFactory(new ServiceCollection().BuildServiceProvider()), null!));
    }
}