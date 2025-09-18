using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Exceptions;
using Ergosfare.Core.Abstractions.Strategies;
using Ergosfare.Test.Fixtures;
using Ergosfare.Test.Fixtures.Stubs.Basic;
using Xunit.Abstractions;

namespace Ergosfare.Core.Test.Strategies;


public class SingleAsyncHandlerMediationStrategyTMessageTests:
    IClassFixture<ExecutionContextFixture>,
    IClassFixture<MessageDependencyFixture>,
    IClassFixture<DescriptorFixture>
{
    private readonly ITestOutputHelper _testOutputHelper;
    private MessageDependencyFixture _messageDependencyFixture;
    private DescriptorFixture _descriptorFixture;
    private readonly ExecutionContextFixture _executionContextFixture;
    // ReSharper disable once ConvertToPrimaryConstructor
    public SingleAsyncHandlerMediationStrategyTMessageTests(
        ITestOutputHelper testOutputHelper,
        MessageDependencyFixture messageDependencyFixture,
        DescriptorFixture descriptorFixture,
        ExecutionContextFixture executionContextFixture)
    {
        _testOutputHelper = testOutputHelper;
        _messageDependencyFixture = messageDependencyFixture;
        _descriptorFixture = descriptorFixture;
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
        _descriptorFixture = _descriptorFixture.New;
        _messageDependencyFixture.RegisterHandler(
            typeof(StubMessage),
            typeof(StubVoidAsyncHandler),
            typeof(StubVoidAsyncPreInterceptor),
            typeof(StubVoidAsyncPostInterceptor),
            typeof(StubVoidAsyncExceptionInterceptor),
            typeof(StubVoidAsyncFinalInterceptor));
        
        
        _descriptorFixture.SetMessageRegistry(_messageDependencyFixture.MessageRegistry);
        var descriptor = _descriptorFixture.GetDescriptorFromRegistry(typeof(StubMessage));
        
        Assert.NotNull(descriptor);
        
        var dependencies = _messageDependencyFixture.CreateDependenciesFromDescriptor<StubMessage>(descriptor!);
        var mediationStrategy = new SingleAsyncHandlerMediationStrategy<StubMessage>(null);

        Assert.NotNull(dependencies);
        Assert.NotEmpty(dependencies.Handlers);
       // act
       var nonExceptionAsync = await Record.ExceptionAsync( async () => 
           await mediationStrategy.Mediate(message, dependencies, _executionContextFixture.Ctx));
        
       // assert
      Assert.Null(nonExceptionAsync);
      _messageDependencyFixture.Dispose();
      _descriptorFixture.Dispose();
    }
    
    [Fact]
    public async Task SingleAsyncHandlerShouldThrowMultipleHandlerException()
    {
        // arrange
        var message = new StubMessage();
        
        _messageDependencyFixture = _messageDependencyFixture.New;
        _descriptorFixture = _descriptorFixture.New;
        
        // @todo Invokers produce problem when invoking sync interceptors eg. converting object to task
        _messageDependencyFixture.RegisterHandler(
            typeof(StubMessage),
            typeof(StubVoidAsyncHandler),
            typeof(StubVoidHandler));
    
         
        _descriptorFixture.SetMessageRegistry(_messageDependencyFixture.MessageRegistry);
        var descriptor = _descriptorFixture.GetDescriptorFromRegistry(typeof(StubMessage));
        
        Assert.NotNull(descriptor);
        
        var dependencies = _messageDependencyFixture.CreateDependenciesFromDescriptor<StubMessage>(descriptor!);
        var mediationStrategy = new SingleAsyncHandlerMediationStrategy<StubMessage>(null);
        
        // act
        var multipleHandlerExceptionAsync = await Record.ExceptionAsync( async () => 
            await mediationStrategy.Mediate(message, dependencies, _executionContextFixture.Ctx));

        Assert.NotNull(multipleHandlerExceptionAsync);
        Assert.IsType<MultipleHandlerFoundException>(multipleHandlerExceptionAsync);
        
        _descriptorFixture.Dispose();   
        _messageDependencyFixture.Dispose();
    }

    [Fact]
    public async Task Mediate_ShouldAssignInvokedResult_WhenHandlerThrowsException()
    {
        // Arrange
        var message = new StubMessage();
        
        _messageDependencyFixture = _messageDependencyFixture.New;
        _descriptorFixture = _descriptorFixture.New;
        var messageDependencies = _messageDependencyFixture
            .RegisterHandler(
                typeof(StubVoidHandlerThrows), // Handler that throws
                typeof(StubVoidAsyncExceptionInterceptor), typeof(StubVoidAsyncFinalInterceptor));
        
        _descriptorFixture.SetMessageRegistry(_messageDependencyFixture.MessageRegistry);
        var descriptor = _descriptorFixture.GetDescriptorFromRegistry(typeof(StubMessage));
        
        Assert.NotNull(descriptor);
        
        var dependencies = _messageDependencyFixture.CreateDependenciesFromDescriptor<StubMessage>(descriptor!);
        var mediationStrategy = new SingleAsyncHandlerMediationStrategy<StubMessage>(null);
        
        

        // Act
        await mediationStrategy.Mediate(message, dependencies, _executionContextFixture.Ctx);

        // Assert
        // Final interceptor ran (ensures finally executed)
        Assert.NotEmpty(dependencies.FinalInterceptors);

        // Optional: verify the exception interceptor ran
        var executedTypes = dependencies.ExceptionInterceptors
            .Select(x => x.Handler.Value.GetType())
            .ToList();

        Assert.Contains(typeof(StubVoidAsyncExceptionInterceptor), executedTypes);
    }
    //
    //
    // [Fact]
    // [Trait("Category", "Coverage")]
    // public async Task SingleAsyncHandlerMediationStrategyTMessage_Mediate_ShouldRunAsyncPreInterceptors()
    // {
    //     
    //     var (_, _, dependencies) = MessageDependencyFixture.CreateMessageDependencies<StubNonGenericMessage>(
    //         [GroupAttribute.DefaultGroupName],
    //         typeof(StubNonGenericHandler),
    //         typeof(StubNonGenericPostInterceptor),
    //         typeof(StubNonGenericStreamExceptionInterceptor),
    //         typeof(StubNonGenericFinalInterceptor));
    //
    //     var strategy = new SingleAsyncHandlerMediationStrategy<StubNonGenericMessage>(new ResultAdapterService());
    //     
    //     await using var _ = AmbientExecutionContext.CreateScope(
    //         StubExecutionContext.Create()
    //     );
    //
    //     var result =  strategy.Mediate(new StubNonGenericMessage(), dependencies, AmbientExecutionContext.Current);
    //
    //     Assert.NotNull(result);
    // }
    
    //
    //
    // [Fact]
    // [Trait("Category", "Coverage")]
    // public async Task SingleAsyncHandlerMediationStrategyTMessage_Mediate_ShouldRunAsyncExceptionInterceptors()
    // {
    //
    //     
    //     var (_, _, dependencies) = MessageDependencyFixture.CreateMessageDependencies<StubNonGenericMessage>(
    //         [GroupAttribute.DefaultGroupName],
    //         typeof(StubNonGenericHandler),
    //         typeof(StubNonGenericPostInterceptor),
    //         typeof(StubNonGenericStreamExceptionInterceptor),
    //         typeof(StubNonGenericFinalInterceptor));
    //     
    //    
    //     
    //     var strategy = new SingleAsyncHandlerMediationStrategy<StubNonGenericMessage>(new ResultAdapterService());
    //     
    //     await using var _ = AmbientExecutionContext.CreateScope(
    //         StubExecutionContext.Create()
    //     );
    //
    //     var result =  strategy.Mediate(new StubNonGenericMessage(), dependencies, AmbientExecutionContext.Current);
    //
    //     Assert.NotNull(result);
    // }
    //
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
    //     var strategy = new SingleAsyncHandlerMediationStrategy<StubMessage>(null);
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
