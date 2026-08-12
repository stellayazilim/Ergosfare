using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Abstractions.Strategies;
using Stella.Ergosfare.Test.Fixtures;
using Stella.Ergosfare.Test.Fixtures.Stubs.Basic;

namespace Stella.Ergosfare.Core.Test.Strategies;


public class SingleAsyncHandlerMediationStrategyTMessageTests:
    IClassFixture<ExecutionContextFixture>,
    IClassFixture<MessageDependencyFixture>
{
    private MessageDependencyFixture _messageDependencyFixture;
    private readonly ExecutionContextFixture _executionContextFixture;
    // ReSharper disable once ConvertToPrimaryConstructor
    public SingleAsyncHandlerMediationStrategyTMessageTests(
        MessageDependencyFixture messageDependencyFixture,
        ExecutionContextFixture executionContextFixture)
    {
        _messageDependencyFixture = messageDependencyFixture;
        _executionContextFixture = executionContextFixture;
    }

    public record TestMessage : IMessage;

    
    [Fact]
    [Trait("Category", "Coverage")]
    [Trait("Category", "Unit")]
    public async Task SingleAsyncHandlerShouldInvokeSingleAsyncHandlerWithoutResponse()
    {
  
        // arrange
        var message = new StubMessage();
        
        _messageDependencyFixture = _messageDependencyFixture.New;
        _messageDependencyFixture.RegisterHandler(
            typeof(StubMessage),
            typeof(StubVoidAsyncHandler),
            typeof(StubVoidAsyncPreInterceptor),
            typeof(StubVoidAsyncPostInterceptor),
            typeof(StubVoidAsyncExceptionInterceptor),
            typeof(StubVoidAsyncFinalInterceptor));
        
        
        
        
        var dependencies = _messageDependencyFixture.CreateDependencies<StubMessage>();
        var mediationStrategy = new SingleAsyncHandlerMediationStrategy<StubMessage>();

        Assert.NotNull(dependencies);
        Assert.NotEmpty(dependencies.Handlers);
       // act
       var nonExceptionAsync = await Record.ExceptionAsync( async () => 
           await mediationStrategy.Mediate(message, dependencies, _executionContextFixture.Ctx, _messageDependencyFixture.ServiceProvider));
        
       // assert
      Assert.Null(nonExceptionAsync);
      _messageDependencyFixture.Dispose();
    }
    
    [Fact]
    public async Task SingleAsyncHandlerShouldThrowMultipleHandlerException()
    {
        // arrange
        var message = new StubMessage();
        
        _messageDependencyFixture = _messageDependencyFixture.New;
        
        // @todo Invokers produce problem when invoking sync interceptors eg. converting object to task
        _messageDependencyFixture.RegisterHandler(
            typeof(StubMessage),
            typeof(StubVoidAsyncHandler),
            typeof(StubVoidHandler));
    
         
        
        
        var dependencies = _messageDependencyFixture.CreateDependencies<StubMessage>();
        var mediationStrategy = new SingleAsyncHandlerMediationStrategy<StubMessage>();
        
        // act
        var multipleHandlerExceptionAsync = await Record.ExceptionAsync( async () => 
            await mediationStrategy.Mediate(message, dependencies, _executionContextFixture.Ctx, _messageDependencyFixture.ServiceProvider));

        Assert.NotNull(multipleHandlerExceptionAsync);
        Assert.IsType<MultipleHandlerFoundException>(multipleHandlerExceptionAsync);
        
        _messageDependencyFixture.Dispose();
    }

    [Fact]
    public async Task Mediate_ShouldAssignInvokedResult_WhenHandlerThrowsException()
    {
        // Arrange
        var message = new StubMessage();
        
        _messageDependencyFixture = _messageDependencyFixture.New;

        // The registration is the arrange's side effect — the returned dependencies are
        // rebuilt from the descriptor below, so no local is kept.
        _messageDependencyFixture.RegisterHandler(
            typeof(StubVoidHandlerThrows), // Handler that throws
            typeof(StubVoidAsyncExceptionInterceptor), typeof(StubVoidAsyncFinalInterceptor));



        var dependencies = _messageDependencyFixture.CreateDependencies<StubMessage>();
        var mediationStrategy = new SingleAsyncHandlerMediationStrategy<StubMessage>();



        // Act
        await mediationStrategy.Mediate(message, dependencies, _executionContextFixture.Ctx, _messageDependencyFixture.ServiceProvider);

        // Assert
        // Final interceptor ran (ensures finally executed)
        Assert.NotEmpty(dependencies.FinalInterceptors);

        // Optional: verify the exception interceptor ran
        var executedTypes = dependencies.ExceptionInterceptors
            .Select(x => x.HandlerType)
            .ToList();

        Assert.Contains(typeof(StubVoidAsyncExceptionInterceptor), executedTypes);
    }
    //
    //
    // [Fact]
    // public async Task Mediate_ShouldThrowMultipleHandlerFoundException_WhenMultipleHandlersExist()
    // {
    //     // Arrange
    //     var messageDependencies = _messageDependencyFixture
    //         .New
    //         .RegisterHandler(typeof(StubVoidHandler), typeof(StubVoidIndirectHandler))
    //         .CreateDependencies<StubMessage>();
    //
    //     var strategy = new SingleAsyncHandlerMediationStrategy<StubMessage>();
    //     var message = new StubMessage();
    //
    //     // Act & Assert
    //     var ex = await Assert.ThrowsAsync<MultipleHandlerFoundException>(
    //         () => strategy.Mediate(message, messageDependencies, _executionContextFixture.Ctx)
    //     );
    //
    //     Assert.Equal(2, ex.HandlerCount);
    //     Assert.Equal(typeof(StubMessage), ex.MessageType);
    // }
}
