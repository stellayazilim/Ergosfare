using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Abstractions.Strategies;
using Stella.Ergosfare.Test.Fixtures;
using Stella.Ergosfare.Test.Fixtures.Stubs.Stream;
using Xunit.Abstractions;

namespace Stella.Ergosfare.Core.Test.Strategies;

/// <summary>
/// Unit tests for <see cref="SingleStreamHandlerMediationStrategy{TMessage, TResult}"/>.
/// </summary>
public class SingleStreamHandlerMediationStrategyTMessageTResultTests :
    IClassFixture<MessageDependencyFixture>,
    IClassFixture<ExecutionContextFixture>
{
    private MessageDependencyFixture _messageDependencyFixture;
    private readonly ExecutionContextFixture _executionContextFixture;

    // ReSharper disable once ConvertToPrimaryConstructor
    public SingleStreamHandlerMediationStrategyTMessageTResultTests(
        ITestOutputHelper testOutputHelper,
        MessageDependencyFixture messageDependencyFixture,
        ExecutionContextFixture executionContextFixture)
    {
        _messageDependencyFixture = messageDependencyFixture;
        _executionContextFixture = executionContextFixture;
    }

    /// <summary>
    /// The strategy should yield every item produced by the single registered stream handler,
    /// in order.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task Mediate_ShouldStreamAllHandlerResults()
    {
        // arrange
        _messageDependencyFixture = _messageDependencyFixture.New;
        _messageDependencyFixture.RegisterHandler(typeof(StubStreamHandler));
        var dependencies = _messageDependencyFixture.CreateDependencies<StubStreamMessage>();
        var strategy = new SingleStreamHandlerMediationStrategy<StubStreamMessage, string>(null, CancellationToken.None);

        // act
        var results = new List<string>();
        await foreach (var item in strategy.Mediate(
                           new StubStreamMessage(), dependencies, _executionContextFixture.Ctx, _messageDependencyFixture.ServiceProvider))
        {
            results.Add(item);
        }

        // assert
        Assert.Equal(StubStreamHandler.Results, results);

        // cleanup
        _messageDependencyFixture.Dispose();
    }

    /// <summary>
    /// Registering two stream handlers for the same message must fail with
    /// <see cref="MultipleHandlerFoundException"/> when enumeration starts.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task Mediate_ShouldThrowMultipleHandlerFoundException()
    {
        // arrange
        _messageDependencyFixture = _messageDependencyFixture.New;
        _messageDependencyFixture.RegisterHandler(
            typeof(StubStreamHandler),
            typeof(DuplicateStubStreamHandler));
        var dependencies = _messageDependencyFixture.CreateDependencies<StubStreamMessage>();
        var strategy = new SingleStreamHandlerMediationStrategy<StubStreamMessage, string>(null, CancellationToken.None);

        // act
        var exception = await Record.ExceptionAsync(async () =>
        {
            await foreach (var _ in strategy.Mediate(
                               new StubStreamMessage(), dependencies, _executionContextFixture.Ctx, _messageDependencyFixture.ServiceProvider))
            {
            }
        });

        // assert
        Assert.IsType<MultipleHandlerFoundException>(exception);

        // cleanup
        _messageDependencyFixture.Dispose();
    }

    /// <summary>
    /// A message with a descriptor but no direct stream handler must fail with an explicit
    /// <see cref="InvalidOperationException"/> when enumeration starts.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task Mediate_ShouldThrow_WhenNoHandlerRegistered()
    {
        // arrange
        _messageDependencyFixture = _messageDependencyFixture.New;
        _messageDependencyFixture.MessageRegistry.Register(typeof(StubStreamMessage));
        var dependencies = _messageDependencyFixture.CreateDependencies<StubStreamMessage>();
        var strategy = new SingleStreamHandlerMediationStrategy<StubStreamMessage, string>(null, CancellationToken.None);

        // act
        var exception = await Record.ExceptionAsync(async () =>
        {
            await foreach (var _ in strategy.Mediate(
                               new StubStreamMessage(), dependencies, _executionContextFixture.Ctx, _messageDependencyFixture.ServiceProvider))
            {
            }
        });

        // assert
        Assert.IsType<InvalidOperationException>(exception);

        // cleanup
        _messageDependencyFixture.Dispose();
    }
}
