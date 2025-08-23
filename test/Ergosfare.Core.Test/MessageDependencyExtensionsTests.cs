using System.Runtime.ExceptionServices;
using Ergosfare.Context;
using Ergosfare.Core.Abstractions.Extensions;
using Ergosfare.Core.Abstractions.Strategies;
using Ergosfare.Core.Internal.Factories;
using Ergosfare.Core.Internal.Mediator;
using Ergosfare.Core.Internal.Registry;
using Ergosfare.Core.Internal.Registry.Descriptors;
using Ergosfare.Core.Test.__stubs__;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Ergosfare.Core.Test;

public class MessageDependencyExtensionsTests
(ITestOutputHelper  testOutputHelper)
{
    
    [Fact]
    [Trait("Category", "Coverage")]
    public void MessageDependenciesExtensionsRunAsyncPreInterceptorsShouldRunPreInterceptors()
    {
        var serviceProvider = new ServiceCollection()
            .AddTransient<StubNonGenericDerivedHandler>()
            .AddTransient<StubNonGenericPreInterceptor>()
            .AddTransient<StubNonGenericDerivedPreInterceptor2>()
            .AddTransient<StubNonGenericDerivedPreInterceptor>()
            .BuildServiceProvider();

        var registry = new MessageRegistry(new HandlerDescriptorBuilderFactory());
        
        registry.Register(typeof(StubNonGenericDerivedHandler));
        registry.Register(typeof(StubNonGenericPreInterceptor));
        registry.Register(typeof(StubNonGenericDerivedPreInterceptor));

        var resolver = new ActualTypeOrFirstAssignableTypeMessageResolveStrategy();
        var descriptor = resolver.Find(typeof(StubNonGenericDerivedMessage), registry);
        var factory = new MessageDependenciesFactory(serviceProvider);
        var dependencies = factory.Create(typeof(StubNonGenericDerivedMessage), descriptor);
    
    
        var res =  
            dependencies
                .RunAsyncPreInterceptors(
                    new StubNonGenericDerivedMessage(), 
                    StubExecutionContext.Create());

        Assert.NotEmpty(dependencies.PreInterceptors);
        Assert.NotEmpty(dependencies.IndirectPreInterceptors);
        Assert.NotNull(res);
    }
    
    

    
    
    
    [Fact]
    [Trait("Category", "Coverage")]
    public async Task MessageDependenciesExtensionsRunAsyncPostInterceptorsShouldRunPostInterceptors()
    {
        // arrange
        var serviceProvider = new ServiceCollection()
            .AddTransient<StubNonGenericDerivedHandler>()
            .AddTransient<StubNonGenericPostInterceptor>()
            .AddTransient<StubNonGenericPostInterceptor2>()
            .AddTransient<StubNonGenericDerivedPostInterceptor>()
            .AddTransient<StubNonGenericDerivedPostInterceptor2>()
            .BuildServiceProvider();


        var registry = new MessageRegistry(new HandlerDescriptorBuilderFactory());
        
        registry.Register(typeof(StubNonGenericDerivedMessage));
        registry.Register(typeof(StubNonGenericDerivedHandler));
        registry.Register(typeof(StubNonGenericPostInterceptor));
        registry.Register(typeof(StubNonGenericPostInterceptor2));
        registry.Register(typeof(StubNonGenericDerivedPostInterceptor));
        registry.Register(typeof(StubNonGenericDerivedPostInterceptor2));


        var resolver = new ActualTypeOrFirstAssignableTypeMessageResolveStrategy();
        
        var descriptor = resolver.Find(typeof(StubNonGenericDerivedMessage), registry);

        var dependencyFactory = new MessageDependenciesFactory(serviceProvider);
        
        // act
        var dependencies = dependencyFactory.Create(typeof(StubNonGenericDerivedMessage), descriptor!);
        
        await using var _ = AmbientExecutionContext.CreateScope(StubExecutionContext.Create());
        await dependencies
                .RunAsyncPostInterceptors(
                    new StubNonGenericDerivedMessage(), 
                    Task.CompletedTask,
                    AmbientExecutionContext.Current);

        // assert
        Assert.NotEmpty(dependencies.PostInterceptors);
        Assert.NotEmpty(dependencies.IndirectPostInterceptors);
    }
    
    
    
    
        
    [Fact]
    [Trait("Category", "Coverage")]
    public async  Task MessageDependenciesExtensionsRunAsyncPostInterceptorsShouldRunIndirectPostInterceptors()
    {
        
        
        var serviceProvider = new ServiceCollection()
            .AddTransient<StubNonGenericPostInterceptor>()
            .BuildServiceProvider();

        var registry = new MessageRegistry(new HandlerDescriptorBuilderFactory());
        
        
        // message descriptor for NonGenericMessage
        var messageDescriptor = new MessageDescriptor(typeof(StubNonGenericDerivedMessage));

        
        // postInterceptor for NonGenericDerivedMessage
        var postInterceptorDescriptor = new HandlerDescriptorBuilderFactory()
            .BuildDescriptors(
                typeof(StubNonGenericPostInterceptor)
            );

        
        // add postIntercepotr as indirect message 
        messageDescriptor.AddDescriptors(postInterceptorDescriptor);
        
        var messageDependencies = new MessageDependencies(
            typeof(StubNonGenericMessage), messageDescriptor, serviceProvider);

        var res = messageDependencies
                .RunAsyncPostInterceptors(
                    new StubNonGenericDerivedMessage(), 
                    Task.CompletedTask,
                    StubExecutionContext.Create());


        Assert.Empty(messageDescriptor.PostInterceptors);
        Assert.NotEmpty(messageDescriptor.IndirectPostInterceptors);
        Assert.NotNull(res);


    }
    
    
    
    
    
    [Fact]
    [Trait("Category", "Coverage")]
    public async  Task MessageDependenciesExtensionsRunAsyncExceptionInterceptorsShouldRunExceptionInterceptors()
    {
        
        
        var serviceProvider = new ServiceCollection()
            .AddTransient<StubNonGenericHandler>()
            .AddTransient<StubNonGenericExceptionInterceptor>()
            .AddTransient<StubNonGenericExceptionInterceptor2>()
            .AddTransient<StubNonGenericDerivedExceptionInterceptor>()
            .BuildServiceProvider();

    
        var registry = new MessageRegistry(new HandlerDescriptorBuilderFactory());
        
        registry.Register(typeof(StubNonGenericMessage));
        registry.Register(typeof(StubNonGenericDerivedMessage));
        registry.Register(typeof(StubNonGenericHandler));
        registry.Register(typeof(StubNonGenericDerivedHandler));
        registry.Register(typeof(StubNonGenericExceptionInterceptor));
        registry.Register(typeof(StubNonGenericExceptionInterceptor2));
        registry.Register(typeof(StubNonGenericDerivedExceptionInterceptor));
        registry.Register(typeof(StubNonGenericDerivedExceptionInterceptor2));
        

        var resolver = new ActualTypeOrFirstAssignableTypeMessageResolveStrategy();
        var descriptor = resolver.Find(typeof(StubNonGenericDerivedMessage), registry);
        var dependencyFactory = new MessageDependenciesFactory(serviceProvider);
        var dependencies = dependencyFactory.Create(typeof(StubNonGenericMessage), descriptor!);
        

        
        
        var res = dependencies
                .RunAsyncExceptionInterceptors(
                    new StubNonGenericDerivedMessage(), 
                    Task.CompletedTask,
                    ExceptionDispatchInfo.Capture(new Exception()), 
                    StubExecutionContext.Create());


    
        
        Assert.NotEmpty(dependencies.ExceptionInterceptors);
        Assert.NotEmpty(dependencies.IndirectExceptionInterceptors);
        
        
        Assert.NotEqual(0, dependencies.ExceptionInterceptors.Count + dependencies.IndirectExceptionInterceptors.Count);
        Assert.NotNull(res);


    }
    
    
    
    
    
    [Fact]
    [Trait("Category", "Coverage")]
    public async  Task MessageDependenciesExtensionsRunAsyncExceptionInterceptorsShouldThrowExceptionDispatchInfo()
    {
        
        
        var serviceProvider = new ServiceCollection()
            .AddTransient<StubNonGenericExceptionInterceptor>()
            .AddTransient<StubNonGenericExceptionInterceptor2>()
            .AddTransient<StubNonGenericDerivedExceptionInterceptor>()
            .AddTransient<StubNonGenericDerivedExceptionInterceptor2>()
            .BuildServiceProvider();

        var messageDescriptor = new MessageDescriptor(typeof(StubNonGenericDerivedMessage));


        
        var messageDependencies = new MessageDependencies(
            typeof(StubNonGenericMessage), messageDescriptor, serviceProvider);

        var res = messageDependencies
                .RunAsyncPostInterceptors(
                    new StubNonGenericDerivedMessage(), 
                    Task.CompletedTask,
                    StubExecutionContext.Create());


        
        Assert.Equal(0, messageDependencies.ExceptionInterceptors.Count + messageDependencies.IndirectExceptionInterceptors.Count);
        
        Assert.Empty(messageDescriptor.ExceptionInterceptors);
        Assert.Empty(messageDependencies.IndirectExceptionInterceptors);
        Assert.NotNull(res);


    }

}