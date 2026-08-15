using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Internal.Mediator;
using Stella.Ergosfare.Test.Fixtures;
using Stella.Ergosfare.Test.Fixtures.Stubs.Basic;

namespace Stella.Ergosfare.Core.Test.Strategies;

/// <summary>
/// Unit tests for <see cref="ResultPipelineBody{TMessage, TResult}"/>.
/// </summary>
public class ResultPipelineBodyTMessageTResultTests :
    IClassFixture<MessageDependencyFixture>,
    IClassFixture<ExecutionContextFixture>
{
    private MessageDependencyFixture _messageDependencyFixture;
    private readonly ExecutionContextFixture _executionContextFixture;

    // ReSharper disable once ConvertToPrimaryConstructor
    public ResultPipelineBodyTMessageTResultTests(
        MessageDependencyFixture messageDependencyFixture,
        ExecutionContextFixture executionContextFixture)
    {
        _messageDependencyFixture = messageDependencyFixture;
        _executionContextFixture = executionContextFixture;
    }

    /// <summary>
    /// With a single handler and no interceptors, the zero-interceptor fast path returns
    /// the handler's result directly.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task Run_ShouldReturnHandlerResult_OnFastPath()
    {
        // arrange
        _messageDependencyFixture = _messageDependencyFixture.New;
        _messageDependencyFixture.RegisterHandler(typeof(StubStringAsyncHandler));
        var dependencies = _messageDependencyFixture.CreateDependencies<StubMessage>();

        // act
        var result = await ResultPipelineBody<StubMessage, string>.Run(
            new StubMessage(), dependencies, null, null, _executionContextFixture.Ctx, _messageDependencyFixture.ServiceProvider);

        // assert
        Assert.Equal(StubStringAsyncHandler.Result, result);

        // cleanup
        _messageDependencyFixture.Dispose();
    }

    /// <summary>
    /// When any interceptor is registered the full pipeline path executes; a final
    /// interceptor must run without altering the handler's result.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task Run_ShouldRunFullPipeline_WhenFinalInterceptorRegistered()
    {
        // arrange
        _messageDependencyFixture = _messageDependencyFixture.New;
        _messageDependencyFixture.RegisterHandler(
            typeof(StubStringAsyncHandler),
            typeof(StubStringAsyncFinalInterceptor));
        var dependencies = _messageDependencyFixture.CreateDependencies<StubMessage>();

        Assert.NotEmpty(dependencies.FinalInterceptors);

        // act
        var result = await ResultPipelineBody<StubMessage, string>.Run(
            new StubMessage(), dependencies, null, null, _executionContextFixture.Ctx, _messageDependencyFixture.ServiceProvider);

        // assert
        Assert.Equal(StubStringAsyncHandler.Result, result);

        // cleanup
        _messageDependencyFixture.Dispose();
    }

    /// <summary>
    /// Two direct handlers for the same message must fail with
    /// <see cref="MultipleHandlerFoundException"/>.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task Run_ShouldThrowMultipleHandlerFoundException()
    {
        // arrange
        _messageDependencyFixture = _messageDependencyFixture.New;
        _messageDependencyFixture.RegisterHandler(
            typeof(StubStringAsyncHandler),
            typeof(StubStringHandler));
        var dependencies = _messageDependencyFixture.CreateDependencies<StubMessage>();

        // act
        var exception = await Record.ExceptionAsync(async () =>
            await ResultPipelineBody<StubMessage, string>.Run(
                new StubMessage(), dependencies, null, null, _executionContextFixture.Ctx, _messageDependencyFixture.ServiceProvider));

        // assert
        Assert.IsType<MultipleHandlerFoundException>(exception);

        // cleanup
        _messageDependencyFixture.Dispose();
    }

    /// <summary>
    /// A message with a descriptor but no direct handler must fail with an explicit
    /// <see cref="NoHandlerFoundException"/>.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task Run_ShouldThrow_WhenNoHandlerRegistered()
    {
        // arrange
        _messageDependencyFixture = _messageDependencyFixture.New;
        _messageDependencyFixture.RegisterHandler(typeof(StubMessage));
        var dependencies = _messageDependencyFixture.CreateDependencies<StubMessage>();

        // act
        var exception = await Record.ExceptionAsync(async () =>
            await ResultPipelineBody<StubMessage, string>.Run(
                new StubMessage(), dependencies, null, null, _executionContextFixture.Ctx, _messageDependencyFixture.ServiceProvider));

        // assert
        Assert.IsType<NoHandlerFoundException>(exception);

        // cleanup
        _messageDependencyFixture.Dispose();
    }

    /// <summary>
    /// A null dependencies argument must fail with <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task Run_ShouldThrowArgumentNullException_WhenDependenciesNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            ResultPipelineBody<StubMessage, string>.Run(
                new StubMessage(), null!, null, null, _executionContextFixture.Ctx, EmptyServiceProviderStub.Instance).AsTask());
    }

    /// <summary>
    /// Minimal <see cref="IServiceProvider"/> for tests that never resolve anything.
    /// </summary>
    private sealed class EmptyServiceProviderStub : IServiceProvider
    {
        public static readonly EmptyServiceProviderStub Instance = new();
        public object? GetService(Type serviceType) => null;
    }
}
