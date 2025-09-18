using Ergosfare.Contracts.Attributes;
using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Internal.Factories;
using Ergosfare.Core.Internal.Mediator;
using Ergosfare.Core.Internal.Registry.Descriptors;
using Ergosfare.Test.Fixtures;
using Ergosfare.Test.Fixtures.Stubs.Basic;
using Ergosfare.Test.Fixtures.Stubs.Generic;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;
// ReSharper disable ConvertToPrimaryConstructor

namespace Ergosfare.Core.Test;

public class MessageDependenciesTest: 
    IClassFixture<MessageDependencyFixture>, IClassFixture<DescriptorFixture>
{
    private readonly ITestOutputHelper _testOutputHelper;
    private MessageDependencyFixture _messageDependencyFixture;
    private readonly DescriptorFixture _descriptorFixture;
    public MessageDependenciesTest(
        DescriptorFixture descriptorFixture,
        MessageDependencyFixture messageDependencyFixture,
        ITestOutputHelper testOutputHelper)
    {
        _descriptorFixture = descriptorFixture;
        _testOutputHelper = testOutputHelper;
        _messageDependencyFixture = messageDependencyFixture;
    }
    
    [Fact]
    [Trait("Category", "Unit")]
    public void MessageDependenciesFactoryShouldCreateMessageDependencies()
    {

        _messageDependencyFixture = _messageDependencyFixture.New;
        _messageDependencyFixture.AddServices(sp => sp.AddTransient<StubVoidHandler>());
        var descriptor = _descriptorFixture.CreateMessageDescriptor<StubMessage>();
  
        var messageDependenciesFactory = new MessageDependenciesFactory(_messageDependencyFixture.ServiceProvider);
        
        // act
        var dependencies = messageDependenciesFactory.Create(typeof(StubMessage), descriptor, []);
        
        // assert 
        Assert.NotNull(dependencies);
        
        // cleanup
        _messageDependencyFixture.Dispose();
    }


    [Fact]
    [Trait("Category", "Coverage")]
    public void MessageDependenciesShouldGetIndirectHandlers()
    {
        // arrange
        var messgeDependencies = new MessageDependencies(
            typeof(IMessage), new MessageDescriptor(typeof(IMessage)), null!, []);
        
        // act
        var indirectHandlers = messgeDependencies.IndirectHandlers;
        
        // assert
        Assert.NotNull(indirectHandlers);
        Assert.Empty(indirectHandlers);
        
        _messageDependencyFixture.Dispose();
    }
    
    private class TestHandler: VoidStubGenericHandler<string> {}
    
     [Fact]
     [Trait("Category", "Coverage")]
     public async Task MessageDependenciesShouldGetHandlerTypeMakeGeneric()
     {
         // Arrange
         // Arrange
        var serviceProvider = new ServiceCollection()
            .AddTransient<VoidStubGenericHandler<string>>()
            .AddTransient<VoidStubGenericPreInterceptor<string>>()
            .AddTransient<VoidStubGenericPostInterceptor<string>>()
            .AddTransient<VoidStubGenericExceptionInterceptor<string>>()
            .AddTransient<VoidStubGenericFinalInterceptor<string>>()
            .BuildServiceProvider();

        // our generic message type to resolve against
       
      
        

        // build descriptor manually
        var handlerDescriptor = new MainHandlerDescriptor()
        {
            Weight = 1,
            Groups = [GroupAttribute.DefaultGroupName],
            MessageType = typeof(StubGenericMessage<>),
            HandlerType = typeof(VoidStubGenericHandler<>),
            ResultType = typeof(Task)
            
        }; 
        
        var preInterceptorDescriptor = new PreInterceptorDescriptor()
        {
            Weight = 1,
            Groups = [GroupAttribute.DefaultGroupName],
            MessageType = typeof(StubGenericMessage<>),
            HandlerType = typeof(VoidStubGenericPreInterceptor<>),
        }; 
        

        var postInterceptorDescriptor = new PostInterceptorDescriptor()
        {
            Weight = 1,
            Groups = [GroupAttribute.DefaultGroupName],
            MessageType = typeof(StubGenericMessage<>),
            HandlerType = typeof(VoidStubGenericPostInterceptor<>),
            ResultType = typeof(Task)
        };
        
        
        var exceptionInterceptorDescriptor = new ExceptionInterceptorDescriptor()
        {
            Weight = 1,
            Groups = [GroupAttribute.DefaultGroupName],
            MessageType = typeof(StubGenericMessage<>),
            HandlerType = typeof(VoidStubGenericExceptionInterceptor<>),
            ResultType = typeof(Task)
        };
        
        var finalInterceptorDescriptor = new FinalInterceptorDescriptor()
        {
            Weight = 1,
            Groups = [GroupAttribute.DefaultGroupName],
            MessageType = typeof(StubGenericMessage<>),
            HandlerType = typeof(VoidStubGenericFinalInterceptor<>),
            ResultType = typeof(Task)
        };

        var messageDescriptor = new MessageDescriptor(typeof(StubGenericMessage<>));
        messageDescriptor.AddDescriptor(handlerDescriptor);
        messageDescriptor.AddDescriptor(preInterceptorDescriptor);
        messageDescriptor.AddDescriptor(postInterceptorDescriptor);
        messageDescriptor.AddDescriptor(exceptionInterceptorDescriptor);
        messageDescriptor.AddDescriptor(finalInterceptorDescriptor);
        var dependencies = new MessageDependencies(
            typeof(StubGenericMessage<string>),
            messageDescriptor,
            serviceProvider, [GroupAttribute.DefaultGroupName]);


        // Assert: should be StubGenericHandler<string>
        Assert.Equal(typeof(VoidStubGenericHandler<string>), dependencies.Handlers.First().Handler.Value.GetType());
        Assert.Equal(typeof(VoidStubGenericPreInterceptor<string>), dependencies.PreInterceptors.First().Handler.Value.GetType());
        Assert.Equal(typeof(VoidStubGenericPostInterceptor<string>), dependencies.PostInterceptors.First().Handler.Value.GetType());
        Assert.Equal(typeof(VoidStubGenericExceptionInterceptor<string>),  dependencies.ExceptionInterceptors.First().Handler.Value.GetType());
        Assert.Equal(typeof(VoidStubGenericFinalInterceptor<string>),  dependencies.FinalInterceptors.First().Handler.Value.GetType());
     }
     
     
     [Fact]
     [Trait("Category", "Coverage")]
     public void MessageDependenciesShouldGetIndirectHandlerType()
     {
         // Arrange
         var serviceProvider = new ServiceCollection()
             .AddTransient<StubVoidHandler>()
             .AddTransient<StubPreInterceptor>()
             .AddTransient<StubPostInterceptor>()
             .AddTransient<StubExceptionInterceptor>()
             .AddTransient<StubFinalInterceptor>()
             .BuildServiceProvider();

         // our generic message type to resolve against


         var messageType = typeof(StubIndirectMessage);
         var indirectMessageType = typeof(StubMessage);
         // build descriptor manually
         var handlerDescriptor = new MainHandlerDescriptor()
         {
             Weight = 1,
             Groups = [GroupAttribute.DefaultGroupName],
             MessageType = indirectMessageType,
             HandlerType = typeof(StubVoidHandler),
             ResultType = typeof(Task)
             
         }; 
         
         var preInterceptorDescriptor = new PreInterceptorDescriptor()
         {
             Weight = 1,
             Groups = [GroupAttribute.DefaultGroupName],
             MessageType = indirectMessageType,
             HandlerType = typeof(StubPreInterceptor),
         }; 


         var postInterceptorDescriptor = new PostInterceptorDescriptor()
         {
             Weight = 1,
             Groups = [GroupAttribute.DefaultGroupName],
             MessageType = indirectMessageType,
             HandlerType = typeof(StubPostInterceptor),
             ResultType = typeof(Task)
         };


         var exceptionInterceptorDescriptor = new ExceptionInterceptorDescriptor()
         {
             Weight = 1,
             Groups = [GroupAttribute.DefaultGroupName],
             MessageType = indirectMessageType,
             HandlerType = typeof(StubExceptionInterceptor),
             ResultType = typeof(Task)
         };

         var finalInterceptorDescriptor = new FinalInterceptorDescriptor()
         {
             Weight = 1,
             Groups = [GroupAttribute.DefaultGroupName],
             MessageType = indirectMessageType,
             HandlerType = typeof(StubFinalInterceptor),
             ResultType = typeof(Task)
         };
         
         var messageDescriptor = new MessageDescriptor(messageType);
         messageDescriptor.AddDescriptor(handlerDescriptor);
         messageDescriptor.AddDescriptor(preInterceptorDescriptor);
         messageDescriptor.AddDescriptor(postInterceptorDescriptor);
         messageDescriptor.AddDescriptor(exceptionInterceptorDescriptor);
         messageDescriptor.AddDescriptor(finalInterceptorDescriptor);
         
         var dependencies = new MessageDependencies(
             messageType,
             messageDescriptor,
             serviceProvider,
             [GroupAttribute.DefaultGroupName]);

         Assert.True(messageType.IsAssignableTo(handlerDescriptor.MessageType));
         Assert.Equal(typeof(StubIndirectMessage),messageDescriptor.MessageType);
        
         Assert.Equal(typeof(StubVoidHandler), dependencies.IndirectHandlers.First().Handler.Value.GetType());
         Assert.Equal(typeof(StubPreInterceptor), dependencies.IndirectPreInterceptors.First().Handler.Value.GetType());
         Assert.Equal(typeof(StubPostInterceptor), dependencies.IndirectPostInterceptors.First().Handler.Value.GetType());
         Assert.Equal(typeof(StubExceptionInterceptor), dependencies.IndirectExceptionInterceptors.First().Handler.Value.GetType());
         Assert.Equal(typeof(StubFinalInterceptor), dependencies.IndirectFinalInterceptors.First().Handler.Value.GetType());
     }
    
    [Fact]
    [Trait("Category", "Coverage")]
    public void MessageDependenciesShouldResolveHandlersWithHandlerType()
    {
        // Arrange
        var serviceProvider = new ServiceCollection()
            .AddTransient<StubVoidHandler>()
            .AddTransient<StubPreInterceptor>()
            .AddTransient<StubPostInterceptor>()
            .AddTransient<StubExceptionInterceptor>()
            .AddTransient<StubFinalInterceptor>()
            .BuildServiceProvider();
        var descriptorFactory = new HandlerDescriptorBuilderFactory();

        var descriptor = new MessageDescriptor(typeof(StubMessage));

        var mainHandlerDescriptor = descriptorFactory.BuildDescriptors(
                typeof(StubVoidHandler)
            );

        var preHandlerDescriptor = descriptorFactory.BuildDescriptors(
            typeof(StubPreInterceptor)
        );

        var postHandlerDescriptor = descriptorFactory.BuildDescriptors(
            typeof(StubPostInterceptor)
            );

        var exceptionHandlerDescriptor = descriptorFactory.BuildDescriptors(
            typeof(StubExceptionInterceptor)
            );

        var finalInterceptorDescriptor = descriptorFactory.BuildDescriptors(
            typeof(StubFinalInterceptor));
        descriptor.AddDescriptors(mainHandlerDescriptor);
        descriptor.AddDescriptors(preHandlerDescriptor);
        descriptor.AddDescriptors(postHandlerDescriptor);
        descriptor.AddDescriptors(exceptionHandlerDescriptor);
        descriptor.AddDescriptors(finalInterceptorDescriptor);
        // Act
        var dependencies = new MessageDependencies(
            typeof(StubMessage),
            descriptor,
            serviceProvider, [GroupAttribute.DefaultGroupName]);

        // Assert: should be StubGenericHandler<string>
        Assert.Equal(typeof(StubVoidHandler), dependencies.Handlers.First().Descriptor.HandlerType);
        Assert.Equal(typeof(StubPreInterceptor), dependencies.PreInterceptors.First().Descriptor.HandlerType);
        Assert.Equal(typeof(StubPostInterceptor), dependencies.PostInterceptors.First().Descriptor.HandlerType);
        Assert.Equal(typeof(StubExceptionInterceptor), dependencies.ExceptionInterceptors.First().Descriptor.HandlerType);
        Assert.Equal(typeof(StubFinalInterceptor), dependencies.FinalInterceptors.First().Descriptor.HandlerType);
    }   
    
     
     [Fact]
     public void MessageDependenciesShouldResolveHandlerInstance()
     {
         // Arrange
         var serviceProvider = new ServiceCollection()
             .AddTransient(typeof(VoidStubGenericHandler<>))
             .BuildServiceProvider();

         var messageType = typeof(StubGenericMessage<string>);
         var handlerType = typeof(VoidStubGenericHandler<string>);

         // register fake handler in service provider
         var handlerInstance = new VoidStubGenericHandler<string>();
        

         var handlerDescriptor =
             new HandlerDescriptorBuilderFactory()
                 .BuildDescriptors(typeof(VoidStubGenericHandler<string>))
                 .First();

         var messageDescriptor = new MessageDescriptor(messageType);
         messageDescriptor.AddDescriptor(handlerDescriptor);

         var deps = new MessageDependencies(
             messageType,
             messageDescriptor,
             serviceProvider, []);

         // Act
         var resolvedHandler = deps.Handlers.First().Handler;

         // Assert
         Assert.NotNull( resolvedHandler);
     }
}