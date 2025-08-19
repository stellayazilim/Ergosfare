using Ergosfare.Context;
using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Exceptions;
using Ergosfare.Core.Abstractions.Strategies;
using Ergosfare.Core.Internal.Factories;
using Ergosfare.Core.Internal.Mediator;
using Ergosfare.Core.Internal.Registry.Descriptors;
using Ergosfare.Test.__stubs__;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit.Abstractions;
using ExecutionContext = Ergosfare.Core.Internal.Contexts.ExecutionContext;

namespace Ergosfare.Core.Test.Strategies;


public class SingleAsyncHandlerMediationStrategyTMessageTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    public SingleAsyncHandlerMediationStrategyTMessageTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    public record TestMessage : IMessage;

    
    [Fact]
    public async Task SingleAsyncHandlerShouldInvokeSingleAsyncHandlerWithoutResponse()
    {
        
       // arrange
       var mockH = Mock.Of<StubHandlers.StubNonGenericHandler>();
       var serviceProvider = new ServiceCollection()
               .AddTransient<StubHandlers.StubNonGenericHandler>()
               .BuildServiceProvider();
    
       var str = new SingleAsyncHandlerMediationStrategy<StubMessages.StubNonGenericMessage>();
       var messageDescriptorBuilderFactory = new HandlerDescriptorBuilderFactory();
       var handlerDescriptors = messageDescriptorBuilderFactory
           .BuildDescriptors(typeof(StubHandlers.StubNonGenericHandler));
       var messageDescriptor = new MessageDescriptor(typeof(StubMessages.StubNonGenericMessage));
       
       // add message descriptors to the message
       messageDescriptor.AddDescriptors(handlerDescriptors);

       var mesageDependencies = new MessageDependencies(
           typeof(StubMessages.StubNonGenericMessage), messageDescriptor, serviceProvider);
        
       // create execution context
       AmbientExecutionContext
           .CreateScope(new ExecutionContext(CancellationToken.None, new Dictionary<object, object?>()));
       
       // act
       var nonExceptionAsync = await Record.ExceptionAsync( async () => await str.Mediate(new StubMessages.StubNonGenericMessage(), mesageDependencies,
           AmbientExecutionContext.Current));
        
       // assert
       Assert.Null(nonExceptionAsync);
    }

    [Fact]
    public async Task SingleAsyncHandlerShouldThrowMultipleHandlerException()
    {
        // arrange
        var mockH = Mock.Of<StubHandlers.StubNonGenericHandler>();
        var serviceProvider = new ServiceCollection()
            
            .AddTransient<StubHandlers.StubNonGenericHandler>()
            .AddTransient<StubHandlers.StubNonGenericHandlerDuplicate>()
            .BuildServiceProvider();
    
        var str = new SingleAsyncHandlerMediationStrategy<StubMessages.StubNonGenericMessage>();
        var messageDescriptorBuilderFactory = new HandlerDescriptorBuilderFactory();
        var messageDescriptor = new MessageDescriptor(typeof(StubMessages.StubNonGenericMessage));
        
        // simulate multiple handler for same message
        foreach (var type in new Type[] {typeof(StubHandlers.StubNonGenericHandler), typeof(StubHandlers.StubNonGenericHandlerDuplicate)})
        {
            var handlerDescriptors = messageDescriptorBuilderFactory
                .BuildDescriptors(type);
            messageDescriptor.AddDescriptors(handlerDescriptors);
        }
      
      
        var mesageDependencies = new MessageDependencies(
            typeof(StubMessages.StubNonGenericMessage), messageDescriptor, serviceProvider);
        
        // create execution context
        AmbientExecutionContext
            .CreateScope(new ExecutionContext(CancellationToken.None, new Dictionary<object, object?>()));
       
        // act
        var exceptionAsync = await Record.ExceptionAsync( async () => await str.Mediate(new StubMessages.StubNonGenericMessage(), mesageDependencies,
            AmbientExecutionContext.Current));
        
        // assert
        Assert.IsAssignableFrom<MultipleHandlerFoundException>(exceptionAsync);
    }
}
