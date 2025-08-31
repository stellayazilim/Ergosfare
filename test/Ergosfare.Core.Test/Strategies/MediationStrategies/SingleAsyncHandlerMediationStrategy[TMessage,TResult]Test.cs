using Ergosfare.Context;
using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Exceptions;
using Ergosfare.Core.Abstractions.Handlers;
using Ergosfare.Core.Abstractions.Strategies;
using Ergosfare.Core.Internal.Factories;
using Ergosfare.Core.Internal.Registry;
using Ergosfare.Core.Internal.Registry.Descriptors;
using Ergosfare.Core.Test.__stubs__;
using Ergosfare.Core.Test.Common;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Ergosfare.Core.Test.Strategies;

public class SingleAsyncHandlerMediationStrategyTests
(ITestOutputHelper testOutputHelper)
{

    private record TestExceptionMessage(bool Throw, string Result) : IMessage;
    
    private record TestExceptionMessageDuplicate(bool Throw, string Result) : TestExceptionMessage(Throw, Result);
    
    private class TestExceptionMessageHandler: IAsyncHandler<TestExceptionMessage, string>
    {
        public Task<string> HandleAsync(TestExceptionMessage message, IExecutionContext context)
        {
            if (message.Throw)
            {
                throw new Exception("TestMessageHandler");
            }
            
            return Task.FromResult(message.Result);
        }
    }
    private class TestExceptionMessageDuplicateHandler: IAsyncHandler<TestExceptionMessage, string>
    {
        public Task<string> HandleAsync(TestExceptionMessage message, IExecutionContext context)
        {
            if (message.Throw)
            {
                throw new Exception("TestMessageHandler");
            }
            
            return Task.FromResult(string.Empty);
        }
    }

    private class TestExceptionMessagePreInterceptor: IAsyncPreInterceptor<TestExceptionMessage>
    {
        public Task HandleAsync(TestExceptionMessage message, IExecutionContext context)
        {
            return Task.CompletedTask;
        }
    }
    
    private class TestExceptionMessagePostInterceptor: IAsyncPostInterceptor<TestExceptionMessage, string>
    {
        public Task HandleAsync(TestExceptionMessage message, string? messageResult, IExecutionContext context)
        {
            return Task.CompletedTask;
        }
    }
    
    private class TestExceptionMessageExceptionInterceptor: IAsyncExceptionInterceptor<TestExceptionMessage, string>
    {
        public Task HandleAsync(TestExceptionMessage message, string? result, Exception exception, IExecutionContext context)
        {
            return Task.CompletedTask;
        }
    }
    
    private class TestExceptionAborterPreInterceptor : IPreInterceptor<StubNonGenericMessage>
    {
     
        public object Handle(StubNonGenericMessage message, IExecutionContext context)
        {
            context.Abort();

            return message;
        }
    }
    
    
    private class TestExceptionAborterResultPreInterceptor : IPreInterceptor<StubNonGenericMessage>
    {
     
        public object Handle(StubNonGenericMessage message, IExecutionContext context)
        {
            context.Abort("foo");

            return message;
        }
    }
    
    
    private class TestUnknownExceptionPreInterceptor : IPreInterceptor<StubNonGenericMessage>
    {
        public object Handle(StubNonGenericMessage message, IExecutionContext context)
        {
            throw new Exception("unknown exception");
        }
    }

    
    private class TestUnknownExceptionInterceptor: IExceptionInterceptor<StubNonGenericMessage, string>
    {
        public object Handle(StubNonGenericMessage message, string? messageResult, Exception exception, IExecutionContext context)
        {
            return Task.FromResult("unknown exception");
        }
    }

    [Fact]
    [Trait("Category", "Coverage")]
    [Trait("Strategy", "Unit")]
    public async Task SingleAsyncHandlerMediationStrategyTMessageTResult_ThrowArgumentNullException()
    {
        var messageDescriptor = new MessageDescriptor(typeof(TestExceptionMessage));

        var strategy = new SingleAsyncHandlerMediationStrategy<TestExceptionMessage, string>();
        
        
        await using( var  _ = AmbientExecutionContext
                  .CreateScope(StubExecutionContext.Create()))
             
        {

            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                strategy.Mediate(new TestExceptionMessage(false, string.Empty), null, AmbientExecutionContext.Current));
        }
    }
    
    
    
    [Fact]
    [Trait("Category", "Coverage")]
    [Trait("Strategy", "Unit")]
    public async Task SingleAsyncHandlerMediationStrategyTMessageTResult_ThrowMultipleHandlerException()
    {

        // arrange
        var serviceProvider = new ServiceCollection()
            .AddTransient<TestExceptionMessageHandler>()
            .AddTransient<TestExceptionMessageDuplicateHandler>()
            .BuildServiceProvider();
        
        var registry = new MessageRegistry(
                new HandlerDescriptorBuilderFactory()
            );
        
        registry.Register(typeof(TestExceptionMessageHandler));
        registry.Register(typeof(TestExceptionMessageDuplicateHandler));

        var resolver = new ActualTypeOrFirstAssignableTypeMessageResolveStrategy();
        var strategy = new SingleAsyncHandlerMediationStrategy<TestExceptionMessage, string>();
        
        var descriptor = resolver.Find(typeof(TestExceptionMessage), registry);
        var dependencies = new MessageDependenciesFactory(serviceProvider)
            .Create(typeof(TestExceptionMessage), descriptor!,[]);
        
        // act
        await using( var  _ = AmbientExecutionContext
                        .CreateScope(StubExecutionContext.Create()))
        {
            // assert
            await Assert.ThrowsAsync<MultipleHandlerFoundException>(() =>
                strategy.Mediate(new TestExceptionMessage(false, string.Empty), dependencies, AmbientExecutionContext.Current));
        }
        
    }
    
    
    
        
    [Fact]
    [Trait("Category", "Coverage")]
    [Trait("Strategy", "Unit")]
    public async Task SingleAsyncHandlerMediationStrategyTMessageTResult_ShoulRunPipeline()
    {

        // arrange
        var serviceProvider = new ServiceCollection()
            .AddTransient<TestExceptionMessageHandler>()
            .AddTransient<TestExceptionMessagePreInterceptor>()
            .AddTransient<TestExceptionMessagePostInterceptor>()
            .AddTransient<TestExceptionMessageExceptionInterceptor>()
            .BuildServiceProvider();
        
        var registry = new MessageRegistry(
            new HandlerDescriptorBuilderFactory()
        );
        
        registry.Register(typeof(TestExceptionMessageHandler));
        registry.Register(typeof(TestExceptionMessagePreInterceptor));
        registry.Register(typeof(TestExceptionMessagePostInterceptor));
        registry.Register(typeof(TestExceptionMessageExceptionInterceptor));

        var resolver = new ActualTypeOrFirstAssignableTypeMessageResolveStrategy();
        var strategy = new SingleAsyncHandlerMediationStrategy<TestExceptionMessage, string>();
        
        var descriptor = resolver.Find(typeof(TestExceptionMessage), registry);
        var dependencies = new MessageDependenciesFactory(serviceProvider)
            .Create(typeof(TestExceptionMessage), descriptor!, []);
        
     
        await using( var  _ = AmbientExecutionContext
                        .CreateScope( StubExecutionContext.Create()))
                
        {
            // act
            var result = await strategy.Mediate(new TestExceptionMessage(false, "test"), dependencies, AmbientExecutionContext.Current);
            
            Assert.Equal("test",result);
            
        }
        
    }
    

    
    [Fact]
    [Trait("Category", "Coverage")]
    [Trait("Strategy", "Unit")]
    public async Task SingleAsyncHandlerMediationStrategy_ShouldThrowInvalidOperationException_WhenHandlerResolvesToNull()
    {

        // Arrange
        
        var dependencies = new StubMessageDependencies();
        var strategy = new SingleAsyncHandlerMediationStrategy<StubNonGenericMessage, string>();

     
        await using (var _ = AmbientExecutionContext.CreateScope( StubExecutionContext.Create() ))
        {
            // act
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                strategy.Mediate(
                    new StubNonGenericMessage(), 
                    dependencies, 
                    AmbientExecutionContext.Current));
            
            // Assert
            Assert.Equal(
                   $"Handler for {nameof(StubNonGenericMessage)} is not of the expected type."
               , ex.Message);
        }
    }

    
    
    [Fact]
    [Trait("Category", "Coverage")]
    public async Task SingleAsyncHandlerMediationStrategy_ShouldThrowWhenExecutionAbortedWithResultValueNull()
    {
        // arrange
        var serviceProvider = new ServiceCollection()
            .AddTransient<StubNonGenericStringResultHandler>()
            .AddTransient<TestExceptionAborterPreInterceptor>()
            .BuildServiceProvider();


        var messageRegistry = new MessageRegistry(new HandlerDescriptorBuilderFactory());
        
        messageRegistry.Register(typeof(StubNonGenericMessage));
        messageRegistry.Register(typeof(StubNonGenericStringResultHandler));
        messageRegistry.Register(typeof(TestExceptionAborterPreInterceptor));
        
        var  resolver = new ActualTypeOrFirstAssignableTypeMessageResolveStrategy();

        var descriptor = resolver.Find(typeof(StubNonGenericMessage), messageRegistry);

        var dependencies = new MessageDependenciesFactory(serviceProvider).Create(
            typeof(StubNonGenericMessage), descriptor!, []);
        
        var mediationStrategy = new SingleAsyncHandlerMediationStrategy<StubNonGenericMessage, string>();


        await using var _ = AmbientExecutionContext.CreateScope(
            StubExecutionContext.Create()
        );

        // Assert that Abort throws
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => mediationStrategy.Mediate(
                new StubNonGenericMessage(),
                dependencies,
                AmbientExecutionContext.Current));

        
        Assert.Equal($"A Message result of type '{nameof(String)}' is required when the execution is aborted as this message has a specific result.", ex.Message);
    }
    
    
    
    
      
    [Fact]
    [Trait("Category", "Coverage")]
    public async Task SingleAsyncHandlerMediationStrategy_ShouldThrowWhenExecutionAbortedWithResultValue()
    {
        // arrange
        var serviceProvider = new ServiceCollection()
            .AddTransient<StubNonGenericStringResultHandler>()
            .AddTransient<TestExceptionAborterResultPreInterceptor>()
            .BuildServiceProvider();


        var messageRegistry = new MessageRegistry(new HandlerDescriptorBuilderFactory());
        
        messageRegistry.Register(typeof(StubNonGenericMessage));
        messageRegistry.Register(typeof(StubNonGenericStringResultHandler));
        messageRegistry.Register(typeof(TestExceptionAborterResultPreInterceptor));
        
        var  resolver = new ActualTypeOrFirstAssignableTypeMessageResolveStrategy();

        var descriptor = resolver.Find(typeof(StubNonGenericMessage), messageRegistry);

        var dependencies = new MessageDependenciesFactory(serviceProvider).Create(
            typeof(StubNonGenericMessage), descriptor!, []);
        
        var mediationStrategy = new SingleAsyncHandlerMediationStrategy<StubNonGenericMessage, string>();


        await using var _ = AmbientExecutionContext.CreateScope(
            StubExecutionContext.Create()
        );

        // Assert that Abort throws
        var result = await mediationStrategy.Mediate(
            new StubNonGenericMessage(),
            dependencies,
            AmbientExecutionContext.Current);

        
        Assert.Equal("foo", result);
    }
    
    
    
    [Fact]
    [Trait("Category", "Coverage")]
    public async Task SingleAsyncHandlerMediationStrategy_ShouldThrowWhenCatchUnknownException()
    {
        // arrange
        var serviceProvider = new ServiceCollection()
            .AddTransient<StubNonGenericStringResultHandler>()
            .AddTransient<TestUnknownExceptionPreInterceptor>()
            .AddTransient<TestUnknownExceptionInterceptor>()
            .BuildServiceProvider();


        var messageRegistry = new MessageRegistry(new HandlerDescriptorBuilderFactory());
        
        messageRegistry.Register(typeof(StubNonGenericMessage));
        messageRegistry.Register(typeof(StubNonGenericStringResultHandler));
        messageRegistry.Register(typeof(TestUnknownExceptionPreInterceptor));
        messageRegistry.Register(typeof(TestUnknownExceptionInterceptor));
        
        var  resolver = new ActualTypeOrFirstAssignableTypeMessageResolveStrategy();

        var descriptor = resolver.Find(typeof(StubNonGenericMessage), messageRegistry);

        var dependencies = new MessageDependenciesFactory(serviceProvider).Create(
            typeof(StubNonGenericMessage), descriptor!, []);
        
        var mediationStrategy = new SingleAsyncHandlerMediationStrategy<StubNonGenericMessage, string>();


        await using var _ = AmbientExecutionContext.CreateScope(
           StubExecutionContext.Create()
        );

        // Assert that Abort throws
        var result = await mediationStrategy.Mediate(
            new StubNonGenericMessage(),
            dependencies,
            AmbientExecutionContext.Current);

        
        Assert.Null(result);
    }
}