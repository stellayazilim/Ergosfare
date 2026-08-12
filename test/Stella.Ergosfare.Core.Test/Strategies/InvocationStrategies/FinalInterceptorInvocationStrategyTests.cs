
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Strategies.InvocationStrategies;
using Stella.Ergosfare.Test.Fixtures;
using Stella.Ergosfare.Test.Fixtures.Stubs.Basic;

namespace Stella.Ergosfare.Core.Test.Strategies.InvocationStrategies;

/// <summary>
/// Unit tests for <see cref="FinalInterceptorInvocationStrategy{TMessage, TResult}"/>.
/// </summary>
public class FinalInterceptorInvocationStrategyTests:
    IClassFixture<MessageDependencyFixture>, 
    IClassFixture<ExecutionContextFixture>
{
    private MessageDependencyFixture _messageDependencyFixture;
    // ReSharper disable once ConvertToPrimaryConstructor
    public FinalInterceptorInvocationStrategyTests(
        MessageDependencyFixture messageDependencyFixture)
    {
        _messageDependencyFixture = messageDependencyFixture;
    }

    
    /// <summary>
    /// Tests that the <see cref="FinalInterceptorInvocationStrategy{TMessage, TResult}"/> correctly executes
    /// both direct and indirect final interceptors for a given message.
    /// </summary>
    /// <remarks>
    /// This test ensures that:
    /// <list type="bullet">
    /// <item>Direct final interceptors are executed for the message.</item>
    /// <item>Indirect final interceptors (registered for parent or assignable messages) are also executed.</item>
    /// <item>The <see cref="MessageDependencyFixture"/> composes the registered participants
    /// into the message's own <see cref="IMessageDependencies"/>.</item>
    /// </list>
    /// </remarks>
    [Fact]
    [Trait("Category", "Coverage")]
    public async Task Invoke_ShouldExecuteDirectAndIndirectFinalInterceptors()
    {
        _messageDependencyFixture = _messageDependencyFixture.New;

        // RegisterHandler registers with the container as well, and building a pipeline
        // verifies its participants are resolvable there.
        _messageDependencyFixture.RegisterHandler(
            typeof(StubIndirectMessage), typeof(StubFinalInterceptor), typeof(StubIndirectFinalInterceptor));

        // StubIndirectFinalInterceptor is declared over the message itself (direct);
        // StubFinalInterceptor is declared over its base and matches covariantly.
        var messageDependencies = _messageDependencyFixture.CreateDependencies<StubIndirectMessage>();

        // Direct and indirect final interceptors are merged into one list, direct first.
        Assert.NotEmpty(messageDependencies.FinalInterceptors);
        Assert.Contains(messageDependencies.FinalInterceptors,
            r => r.HandlerType == typeof(StubFinalInterceptor));
        Assert.Contains(messageDependencies.FinalInterceptors,
            r => r.HandlerType == typeof(StubIndirectFinalInterceptor));
        
        // cleanup
        _messageDependencyFixture.Dispose();

    }
    
 


}