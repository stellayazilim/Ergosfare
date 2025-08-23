using Ergosfare.Context;
using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Exceptions;
using Ergosfare.Core.Abstractions.Strategies;
using Ergosfare.Core.Internal.Factories;
using Ergosfare.Core.Internal.Mediator;
using Ergosfare.Core.Internal.Registry;
using Ergosfare.Core.Internal.Registry.Descriptors;
using Ergosfare.Core.Test.__factories__;
using Ergosfare.Core.Test.__stubs__;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

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
       var serviceProvider = new ServiceCollection()
               .AddTransient<StubNonGenericHandler>()
               .AddTransient<StubNonGenericPostInterceptor>()
               .AddTransient<StubNonGenericPostInterceptor2>()
               .AddTransient<StubNonGenericExceptionInterceptor>()
               .AddTransient<StubNonGenericExceptionInterceptor2>()
               .AddTransient<StubNonGenericDerivedPostInterceptor>()
               .BuildServiceProvider();
    
       var str = new SingleAsyncHandlerMediationStrategy<StubNonGenericDerivedMessage>();
       var messageDescriptorBuilderFactory = new HandlerDescriptorBuilderFactory();
       
       
       // @todo refactor with <see cref="MessageRegistry" />
       var handlerDescriptors = messageDescriptorBuilderFactory
           .BuildDescriptors(typeof(StubNonGenericHandler));
       var postInterceptorDescriptors = messageDescriptorBuilderFactory
           .BuildDescriptors(typeof(StubNonGenericPostInterceptor));
       var postInterceptorDescriptors2 = messageDescriptorBuilderFactory
           .BuildDescriptors(typeof(StubNonGenericPostInterceptor2));
       var errorInterceptorDescriptors = messageDescriptorBuilderFactory
           .BuildDescriptors(typeof(StubNonGenericExceptionInterceptor));
       var errorInterceptorDescriptors2 = messageDescriptorBuilderFactory
           .BuildDescriptors(typeof(StubNonGenericExceptionInterceptor2));
       var errorInterceptorDescriptors3 = messageDescriptorBuilderFactory
           .BuildDescriptors(typeof(StubNonGenericDerivedPostInterceptor));
       var messageDescriptor = new MessageDescriptor(typeof(StubNonGenericDerivedMessage));
       
       // add message descriptors to the message
       messageDescriptor.AddDescriptors(handlerDescriptors);
       messageDescriptor.AddDescriptors(postInterceptorDescriptors);
       messageDescriptor.AddDescriptors(postInterceptorDescriptors2);
       messageDescriptor.AddDescriptors(errorInterceptorDescriptors);
       messageDescriptor.AddDescriptors(errorInterceptorDescriptors2);
       messageDescriptor.AddDescriptors(errorInterceptorDescriptors3);
       
       var messageDependencies = new MessageDependencies(
           typeof(StubNonGenericDerivedMessage), messageDescriptor, serviceProvider);
        
       // create execution context
       AmbientExecutionContext
           .CreateScope(StubExecutionContext.Create());
       
       // act
       var nonExceptionAsync = await Record.ExceptionAsync( async () => await str.Mediate(new StubNonGenericDerivedMessage(), messageDependencies,
           AmbientExecutionContext.Current));
        
       // assert
       Assert.Null(nonExceptionAsync);
    }

    [Fact]
    public async Task SingleAsyncHandlerShouldThrowMultipleHandlerException()
    {
        // arrange
        var serviceProvider = new ServiceCollection()
            
            .AddTransient<StubNonGenericHandler>()
            .AddTransient<StubNonGenericHandler2>()
            .BuildServiceProvider();
    
        var str = new SingleAsyncHandlerMediationStrategy<StubNonGenericMessage>();
        var messageDescriptorBuilderFactory = new HandlerDescriptorBuilderFactory();
        var messageDescriptor = new MessageDescriptor(typeof(StubNonGenericMessage));
        
        // simulate multiple handler for same message
        foreach (var type in new Type[] {typeof(StubNonGenericHandler), typeof(StubNonGenericHandler2)})
        {
            var handlerDescriptors = messageDescriptorBuilderFactory
                .BuildDescriptors(type);
            messageDescriptor.AddDescriptors(handlerDescriptors);
        }
      
      
        var mesageDependencies = new MessageDependencies(
            typeof(StubNonGenericMessage), messageDescriptor, serviceProvider);
        
        // create execution context
        AmbientExecutionContext
            .CreateScope(StubExecutionContext.Create());
       
        // act
        var exceptionAsync = await Record.ExceptionAsync( async () => await str.Mediate(new StubNonGenericMessage(), mesageDependencies,
            AmbientExecutionContext.Current));
        
        // assert
        Assert.IsAssignableFrom<MultipleHandlerFoundException>(exceptionAsync);
    }



    [Fact]
    [Trait("Category", "Coverage")]
    public async Task SingleAsyncHandlerMediationStrategyTMessage_Mediate_ShouldRunAsyncPreInterceptors()
    {

        var (dependencies, _, _) = SingleMessageDependencyMediationFactory.Create<StubNonGenericMessage>(
            typeof(StubNonGenericHandler),
            typeof(StubNonGenericPostInterceptor),
            typeof(StubNonGenericStreamExceptionInterceptor));
        
        var strategy = new SingleAsyncHandlerMediationStrategy<StubNonGenericMessage>();
        
        await using var _ = AmbientExecutionContext.CreateScope(
            StubExecutionContext.Create()
        );

        var result =  strategy.Mediate(new StubNonGenericMessage(), dependencies, AmbientExecutionContext.Current);

        Assert.NotNull(result);
    }
    
    
    
    [Fact]
    [Trait("Category", "Coverage")]
    public async Task SingleAsyncHandlerMediationStrategyTMessage_Mediate_ShouldRunAsyncExceptionInterceptors()
    {

        var (dependencies, _, _) = SingleMessageDependencyMediationFactory.Create<StubNonGenericMessage>(
            typeof(StubNonGenericHandler),
            typeof(StubNonGenericPostInterceptor),
            typeof(StubNonGenericStreamExceptionInterceptor));
        
        var strategy = new SingleAsyncHandlerMediationStrategy<StubNonGenericMessage>();
        
        await using var _ = AmbientExecutionContext.CreateScope(
            StubExecutionContext.Create()
        );

        var result =  strategy.Mediate(new StubNonGenericMessage(), dependencies, AmbientExecutionContext.Current);

        Assert.NotNull(result);
    }
}
