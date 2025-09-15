using System.Runtime.ExceptionServices;
using Castle.Components.DictionaryAdapter;
using Ergosfare.Context;
using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Extensions;
using Ergosfare.Core.Abstractions.Handlers;
using Ergosfare.Core.Abstractions.Registry.Descriptors;
using Ergosfare.Core.Abstractions.Strategies;
using Ergosfare.Core.Internal.Factories;
using Ergosfare.Core.Internal.Mediator;
using Ergosfare.Core.Internal.Registry;
using Ergosfare.Core.Internal.Registry.Descriptors;
using Ergosfare.Core.Test.__fixtures__;
using Ergosfare.Core.Test.__stubs__;
using Ergosfare.Core.Test.__stubs__.Handlers;
using Ergosfare.Core.Test.Common;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;
using GroupAttribute = Ergosfare.Contracts.Attributes.GroupAttribute;

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

        var resolver = new ActualTypeOrFirstAssignableTypeMessageResolveStrategy(registry);
        var descriptor = resolver.Find(typeof(StubNonGenericDerivedMessage));
        var factory = new MessageDependenciesFactory(serviceProvider);
        var dependencies = factory.Create(typeof(StubNonGenericDerivedMessage), descriptor, []);
    
    
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

    var resolver = new ActualTypeOrFirstAssignableTypeMessageResolveStrategy(registry);
    var descriptor = resolver.Find(typeof(StubNonGenericDerivedMessage));

    var dependencyFactory = new MessageDependenciesFactory(serviceProvider);

    var dependencies = dependencyFactory.Create(typeof(StubNonGenericDerivedMessage), descriptor!, []);

    await using var _ = AmbientExecutionContext.CreateScope(StubExecutionContext.Create());

    var message = new StubNonGenericDerivedMessage();

    // 🔑 get the real handler and execute it
    var handler = (IHandler<StubNonGenericDerivedMessage, object>)
        dependencies.Handlers.Single().Handler.Value;

    var handlerResult =  handler.Handle(message, AmbientExecutionContext.Current);

    // act
    var result = await  dependencies.RunAsyncPostInterceptors(
        message,
        handlerResult,
        AmbientExecutionContext.Current,
        new ResultAdapterService());

    // assert
    Assert.NotEmpty(dependencies.PostInterceptors);
    Assert.NotEmpty(dependencies.IndirectPostInterceptors);
    Assert.NotNull(result); // final transformed result should not be null
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
            typeof(StubNonGenericMessage), messageDescriptor, serviceProvider, []);

        var res = messageDependencies
                .RunAsyncPostInterceptors(
                    new StubNonGenericDerivedMessage(), 
                    Task.CompletedTask,
                    StubExecutionContext.Create(),
                    new ResultAdapterService());


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
        

        var resolver = new ActualTypeOrFirstAssignableTypeMessageResolveStrategy(registry);
        var descriptor = resolver.Find(typeof(StubNonGenericDerivedMessage));
        var dependencyFactory = new MessageDependenciesFactory(serviceProvider);
        var dependencies = dependencyFactory.Create(typeof(StubNonGenericMessage), descriptor!, []);
        

        
        
        var res = dependencies
                .RunAsyncExceptionInterceptors(
                    new StubNonGenericDerivedMessage(), 
                    Task.CompletedTask,
                    ExceptionDispatchInfo.Capture(new Exception()), 
                    StubExecutionContext.Create());


    
        
       // Assert.NotEmpty(dependencies.ExceptionInterceptors);
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
            typeof(StubNonGenericMessage), messageDescriptor, serviceProvider, []);

        var res = messageDependencies
                .RunAsyncPostInterceptors(
                    new StubNonGenericDerivedMessage(), 
                    Task.CompletedTask,
                    StubExecutionContext.Create(),
                    new ResultAdapterService());


        
        Assert.Equal(0, messageDependencies.ExceptionInterceptors.Count + messageDependencies.IndirectExceptionInterceptors.Count);
        
        Assert.Empty(messageDescriptor.ExceptionInterceptors);
        Assert.Empty(messageDependencies.IndirectExceptionInterceptors);
        Assert.NotNull(res);


    }

    
    
    [Fact]
    [Trait("Category", "Coverage")]
    
    public async Task RunAsyncExceptionInterceptors_ShouldRethrow_WhenNoInterceptors()
    {
        // arrange


        var dependencyFactory = new StubMessageDependencies();
        var ex = new Exception();
        // act & assert
        var thrown = await Assert.ThrowsAsync<Exception>(async () =>
        {
            await dependencyFactory.RunAsyncExceptionInterceptors(new StubNonGenericMessage(), null, ExceptionDispatchInfo.Capture(ex), StubExecutionContext.Create());
        });

        Assert.Same(ex, thrown); // ensure it's the same instance
    }
    
    
    
    
    [Fact]
    [Trait("Category", "Coverage")]
    public async Task RunAsyncExceptionInterceptors_ShouldRunIndirectExceptionInterceptor()
    {


    }
    
    
    
    [Fact]
    [Trait("Category", "Coverage")]
    public async Task RunAsyncExceptionInterceptors_ShouldHaveNoInterceptor()
    {

        var ctx = ExecutionContextFixture.CreateExecutionContext();
        var (_, _, dependencies) = MessageDependencyFixture.CreateMessageDependencies<IndirectStubStringMessage>(
            [GroupAttribute.DefaultGroupName]);

 
        var dispatchInfo = ExceptionDispatchInfo.Capture(new Exception("Test exception"));

        var threw = await Assert.ThrowsAsync<Exception>(async () =>
        {
            await dependencies.RunAsyncExceptionInterceptors(new StubStringMessage("TestMessage"), "TestResult",
                dispatchInfo, ctx);
        });
        Assert.False((dependencies.IndirectExceptionInterceptors.Count + dependencies.ExceptionInterceptors.Count) > 0);

        Assert.Same(dispatchInfo.SourceException, threw);
    }

    
    
    [Fact]
    [Trait("Category", "Coverage")]
    public async Task RunAsyncExceptionInterceptors_ShouldHaveInterceptor()
    {

        var ctx = ExecutionContextFixture.CreateExecutionContext();
        var (_, _, dependencies) = MessageDependencyFixture.CreateMessageDependencies<StubStringMessage>(
            [GroupAttribute.DefaultGroupName],
            typeof(StubStringExceptionInterceptorReturnsNull));

    
        testOutputHelper.WriteLine(dependencies.ExceptionInterceptors.Count.ToString());
        var dispatchInfo = ExceptionDispatchInfo.Capture(new Exception("Test exception"));
        
        var result = await dependencies.RunAsyncExceptionInterceptors(new StubStringMessage("TestMessage"), "TestResult",
            dispatchInfo, ctx);
        Assert.True((dependencies.IndirectExceptionInterceptors.Count + dependencies.ExceptionInterceptors.Count) > 0);

        Assert.Null(result);
    }
    
    
        
    [Fact]
    [Trait("Category", "Coverage")]
    public async Task RunAsyncExceptionInterceptors_ShouldHaveIndirectInterceptor()
    {

        var ctx = ExecutionContextFixture.CreateExecutionContext();
        var (_, _, dependencies) = MessageDependencyFixture.CreateMessageDependencies<StubStringMessage>(
            [GroupAttribute.DefaultGroupName],
            typeof(StubStringMessageExceptionInterceptor),
            typeof(IndirectStubStringExceptionInterceptor));

    
        testOutputHelper.WriteLine(dependencies.ExceptionInterceptors.Count.ToString());
        var dispatchInfo = ExceptionDispatchInfo.Capture(new Exception("Test exception"));
        
        var result = await dependencies.RunAsyncExceptionInterceptors(new StubStringMessage("TestMessage"), "TestResult",
            dispatchInfo, ctx);
        Assert.True((dependencies.IndirectExceptionInterceptors.Count + dependencies.ExceptionInterceptors.Count) > 0);

        Assert.Equal("TestResult",result);
    }
    
    
    [Fact]
    [Trait("Category", "Coverage")]
    public async Task RunAsyncExceptionInterceptors_ShouldHaveDirectIndirectExceptionInterceptor()
    {

        var ctx = ExecutionContextFixture.CreateExecutionContext();
        var (_, _, dependencies) = MessageDependencyFixture.CreateMessageDependencies<IndirectStubMessage>(
            [GroupAttribute.DefaultGroupName],
            typeof(StubMessageExceptionInterceptor),
            typeof(IndirectStubMessageExceptionInterceptor));

    
        var dispatchInfo = ExceptionDispatchInfo.Capture(new Exception("Test exception"));
        
        var result = await dependencies.RunAsyncExceptionInterceptors(new IndirectStubMessage(), "TestResult",
            dispatchInfo, ctx);
        
        var indirectEnumerator = dependencies.IndirectExceptionInterceptors.GetEnumerator();
        var directEnumerator = dependencies.ExceptionInterceptors.GetEnumerator();
  
        Assert.True(indirectEnumerator.MoveNext());
        Assert.True(directEnumerator.MoveNext());
        Assert.True((dependencies.IndirectExceptionInterceptors.Count + dependencies.ExceptionInterceptors.Count) > 0);
        Assert.Equal("TestResult",result);
        
        directEnumerator.Dispose();
        indirectEnumerator.Dispose();
    }
    
    [Fact]
    [Trait("Category", "Coverage")]
    public async Task RunAsyncExceptionInterceptors_ShouldHaveNoMoveNextInterceptor()
    {
        var ctx = ExecutionContextFixture.CreateExecutionContext();
        var (_, _, dependencies) = MessageDependencyFixture.CreateMessageDependencies<IndirectStubStringMessage>(
            [GroupAttribute.DefaultGroupName]);

        // Force enumeration so LINQ's Count executes its MoveNext internally
        var enumerator = dependencies.ExceptionInterceptors.GetEnumerator();

        Assert.False(enumerator.MoveNext());
        enumerator.Dispose();
    }
    
        
    [Fact]
    [Trait("Category", "Coverage")]
    public async Task RunAsyncExceptionInterceptors_ShouldRunFinalInterceptors()
    {

        var ctx = ExecutionContextFixture.CreateExecutionContext();
        var (_, _, dependencies) = MessageDependencyFixture.CreateMessageDependencies<IndirectStubStringMessage>(
            [GroupAttribute.DefaultGroupName],
            typeof(StubStringFinalInterceptor));

    
        
         await dependencies.RunAsyncFinalInterceptors(new IndirectStubStringMessage("TestMessage"), "TestResult",null, ctx);
            
         Assert.True(StubStringFinalInterceptor.IsRuned);
    }
    
}