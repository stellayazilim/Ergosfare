using System.Reflection;
using Ergosfare.Core.Abstractions.Invokers;
using Ergosfare.Core.Abstractions.Strategies.InvocationStrategies;
using Ergosfare.Test.Fixtures;
using Ergosfare.Test.Fixtures.Stubs.Basic;

namespace Ergosfare.Core.Test.Strategies.InvocationStrategies;

public class PreInvocationStrategyTests: 
    IClassFixture<ResultAdapterFixtures>,
    IClassFixture<MessageDependencyFixture>
{
    private readonly MessageDependencyFixture _messageDependencyFixture;
    private readonly ResultAdapterFixtures _resultAdapterFixture;
    // ReSharper disable once ConvertToPrimaryConstructor
    public PreInvocationStrategyTests(
        MessageDependencyFixture messageDependencyFixture,
        ResultAdapterFixtures resultAdapterFixtures)
    {
        _messageDependencyFixture = messageDependencyFixture;
        _resultAdapterFixture = resultAdapterFixtures;
    }
    [Fact]
    [Trait("Category", "Coverage")]
    public void PostInvocationStrategy_ShouldExposeResultAdapterService()
    {
        // Arrange
        var messageDependencies = _messageDependencyFixture.CreateDependencies<StubMessage>();
        var resultAdapterService = _resultAdapterFixture.ResultAdapterService;

        // Act
        var strategy = new TaskPostInterceptorInvocationStrategy(messageDependencies, resultAdapterService);

        // Use reflection to access the protected property
        var property = typeof(AbstractInvoker)
            .GetProperty("ResultAdapterService", BindingFlags.NonPublic | BindingFlags.Instance);
        var value = property!.GetValue(strategy);

        // Assert
        Assert.Same(resultAdapterService, value);
    }
}