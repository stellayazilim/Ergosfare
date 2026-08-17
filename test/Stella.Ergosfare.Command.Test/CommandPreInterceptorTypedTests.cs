using Stella.Ergosfare.Command.Test.__stubs__;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Command.Test;


/// <summary>
/// Verifies that the typed command pre-interceptor's default implementation forwards the
/// untyped pipeline call to its type-safe member, and that the command it returns is what
/// travels on.
/// </summary>
public class CommandPreInterceptorTypedTests
{

    /// <summary>
    /// A pre-interceptor that returns the command it was given.
    /// </summary>
    // Invoked directly below, never dispatched — deliberately outside the compiled closure.
    [ExcludeFromDiscovery]
    private class TestTypedPreInterceptor : ICommandPreInterceptor<StubNonGenericCommand>
    {
        public ValueTask<StubNonGenericCommand> HandleAsync(StubNonGenericCommand command, ErgosfareContext context)
        {
            return ValueTask.FromResult(command);
        }
    }


    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task TypedPreInterceptorDefaultImplementation_ShouldForwardAndReturnTheCommand()
    {
        // arrange
        var command = new StubNonGenericCommand();
        IAsyncPreInterceptor<StubNonGenericCommand> interceptor = new TestTypedPreInterceptor();

        // act
        var result = await interceptor.HandleAsync(command, new ErgosfareContext());

        // assert
        Assert.Same(command, result);
    }
}
