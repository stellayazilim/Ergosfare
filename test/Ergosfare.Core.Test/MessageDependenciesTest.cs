using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Internal.Factories;
using Ergosfare.Core.Internal.Mediator;
using Ergosfare.Core.Internal.Registry.Descriptors;
using Ergosfare.Test.__stubs__;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Ergosfare.Core.Test;

public class MessageDependenciesTest
{

    [Fact]
    [Trait("Category", "Unit")]
    public void MessageDependenciesFactoryShouldCreateMessageDependencies()
    {
        // arrange 
        var serviceCollection = new ServiceCollection().BuildServiceProvider();
        var descriptor = new MessageDescriptor(typeof(StubMessages.StubNonGenericMessage));
        var messageDependenciesFactory = new MessageDependenciesFactory(serviceCollection);

        
        
        // act
        var dependencies = messageDependenciesFactory.Create(typeof(StubMessages.StubNonGenericMessage), descriptor);
        
        // assert 
        Assert.NotNull(dependencies);
    }


    [Fact]
    [Trait("Category", "Coverage")]
    public void MessageDependenciesShouldGetIndirectHandlers()
    {
        // arrange
        var messgeDependencies = new MessageDependencies(
            typeof(IMessage), new MessageDescriptor(typeof(IMessage)), null!);
        
        // act
        var indirectHandlers = messgeDependencies.IndirectHandlers;
        
        // assert
        Assert.NotNull(indirectHandlers);
        Assert.Empty(indirectHandlers);
    }



    [Fact]
    [Trait("Category", "Coverage")]
    public void MessageDependenciesShouldGetHandlerTypeMakeGeneric()
    {
        // Arrange
        var serviceProvider = new ServiceCollection()
            .AddTransient<StubHandlers.StubGenericHandler<string>>()
            .AddTransient<StubHandlers.StubGenericPreInterceptor<string>>()
            .AddTransient<StubHandlers.StubGenericPostInterceptor<string>>()
            .AddTransient<StubHandlers.StubGenericExceptionInterceptor<string>>()
            .BuildServiceProvider();

        // our generic message type to resolve against
       
      
        

        // build descriptor manually
        var handlerDescriptor = new MainHandlerDescriptor()
        {
            MessageType = typeof(StubMessages.StubGenericMessage<>),
            HandlerType = typeof(StubHandlers.StubGenericHandler<>),
            ResultType = typeof(Task)
            
        }; 
        
        var preInterceptorDescriptor = new PreInterceptorDescriptor()
        {
            MessageType = typeof(StubMessages.StubGenericMessage<>),
            HandlerType = typeof(StubHandlers.StubGenericPreInterceptor<>),
        }; 


        var postInterceptorDescriptor = new PostInterceptorDescriptor()
        {
            MessageType = typeof(StubMessages.StubGenericMessage<>),
            HandlerType = typeof(StubHandlers.StubGenericPostInterceptor<>),
            ResultType = typeof(Task)
        };


        var exceptionInterceptorDescriptor = new ExceptionInterceptorDescriptor()
        {
            MessageType = typeof(StubMessages.StubGenericMessage<>),
            HandlerType = typeof(StubHandlers.StubGenericExceptionInterceptor<>),
            ResultType = typeof(Task)
        };

        var messageDescriptor = new MessageDescriptor(typeof(StubMessages.StubGenericMessage<>));
        messageDescriptor.AddDescriptor(handlerDescriptor);
        messageDescriptor.AddDescriptor(preInterceptorDescriptor);
        messageDescriptor.AddDescriptor(postInterceptorDescriptor);
        messageDescriptor.AddDescriptor(exceptionInterceptorDescriptor);
        
        var dependencies = new MessageDependencies(
            typeof(StubMessages.StubGenericMessage<string>),
            messageDescriptor,
            serviceProvider);


        // Assert: should be StubGenericHandler<string>
        Assert.Equal(typeof(StubHandlers.StubGenericHandler<string>), dependencies.Handlers.First().Handler.Value.GetType());
        Assert.Equal(typeof(StubHandlers.StubGenericPreInterceptor<string>), dependencies.PreInterceptors.First().Handler.Value.GetType());
        Assert.Equal(typeof(StubHandlers.StubGenericPostInterceptor<string>), dependencies.PostInterceptors.First().Handler.Value.GetType());
        Assert.Equal(typeof(StubHandlers.StubGenericExceptionInterceptor<string>),  dependencies.ExceptionInterceptors.First().Handler.Value.GetType());
    }
    
    
       [Fact]
    [Trait("Category", "Coverage")]
    public void MessageDependenciesShouldGetIndirectHandlerType()
    {
        // Arrange
        var serviceProvider = new ServiceCollection()
            .AddTransient<StubHandlers.StubNonGenericHandler>()
            .AddTransient<StubHandlers.StubNonGenericPreInterceptor>()
            .AddTransient<StubHandlers.StubNonGenericPostInterceptor>()
            .AddTransient<StubHandlers.StubNonGenericExceptionInterceptor>()
            .BuildServiceProvider();

        // our generic message type to resolve against


        var messageType = typeof(StubMessages.StubNonGenericDerivedMessage);
        var indirectMessageType = typeof(StubMessages.StubNonGenericMessage);
        // build descriptor manually
        var handlerDescriptor = new MainHandlerDescriptor()
        {
            MessageType = indirectMessageType,
            HandlerType = typeof(StubHandlers.StubNonGenericHandler),
            ResultType = typeof(Task)
            
        }; 
        
        var preInterceptorDescriptor = new PreInterceptorDescriptor()
        {
            MessageType = indirectMessageType,
            HandlerType = typeof(StubHandlers.StubNonGenericPreInterceptor),
        }; 


        var postInterceptorDescriptor = new PostInterceptorDescriptor()
        {
            MessageType = indirectMessageType,
            HandlerType = typeof(StubHandlers.StubNonGenericPostInterceptor),
            ResultType = typeof(Task)
        };


        var exceptionInterceptorDescriptor = new ExceptionInterceptorDescriptor()
        {
            MessageType = indirectMessageType,
            HandlerType = typeof(StubHandlers.StubNonGenericExceptionInterceptor),
            ResultType = typeof(Task)
        };
            
        var messageDescriptor = new MessageDescriptor(messageType);
        messageDescriptor.AddDescriptor(handlerDescriptor);
        messageDescriptor.AddDescriptor(preInterceptorDescriptor);
        messageDescriptor.AddDescriptor(postInterceptorDescriptor);
        messageDescriptor.AddDescriptor(exceptionInterceptorDescriptor);
        
        
        var dependencies = new MessageDependencies(
            messageType,
            messageDescriptor,
            serviceProvider);

        Assert.True(messageType.IsAssignableTo(handlerDescriptor.MessageType));
        Assert.Equal(typeof(StubMessages.StubNonGenericDerivedMessage),messageDescriptor.MessageType);
        // Assert: should be StubGenericHandler<string>
        Assert.Equal(typeof(StubHandlers.StubNonGenericHandler), dependencies.IndirectHandlers.First().Handler.Value.GetType());
        Assert.Equal(typeof(StubHandlers.StubNonGenericPreInterceptor), dependencies.IndirectPreInterceptors.First().Handler.Value.GetType());
        Assert.Equal(typeof(StubHandlers.StubNonGenericPostInterceptor), dependencies.IndirectPostInterceptors.First().Handler.Value.GetType());
        Assert.Equal(typeof(StubHandlers.StubNonGenericExceptionInterceptor), dependencies.IndirectExceptionInterceptors.First().Handler.Value.GetType());
    }
    
    [Fact]
    [Trait("Category", "Coverage")]
    public void MessageDependenciesShouldResolveHandlersWithHandlerType()
    {
        // Arrange
        var serviceProvider = new ServiceCollection()
            .AddTransient<StubHandlers.StubNonGenericHandler>()
            .AddTransient<StubHandlers.StubNonGenericPreInterceptor>()
            .AddTransient<StubHandlers.StubNonGenericPostInterceptor>()
            .AddTransient<StubHandlers.StubNonGenericExceptionInterceptor>()
            .BuildServiceProvider();
        var descriptorFactory = new HandlerDescriptorBuilderFactory();

        var descriptor = new MessageDescriptor(typeof(StubMessages.StubNonGenericMessage));

        var mainHandlerDescriptor = descriptorFactory.BuildDescriptors(
                typeof(StubHandlers.StubNonGenericHandler)
            );

        var preHandlerDescriptor = descriptorFactory.BuildDescriptors(
            typeof(StubHandlers.StubNonGenericPreInterceptor)
        );

        var postHandlerDescriptor = descriptorFactory.BuildDescriptors(
            typeof(StubHandlers.StubNonGenericPostInterceptor)
            );

        var exceptionHandlerDescriptor = descriptorFactory.BuildDescriptors(
            typeof(StubHandlers.StubNonGenericExceptionInterceptor)
            );
        descriptor.AddDescriptors(mainHandlerDescriptor);
        descriptor.AddDescriptors(preHandlerDescriptor);
        descriptor.AddDescriptors(postHandlerDescriptor);
        descriptor.AddDescriptors(exceptionHandlerDescriptor);

        // Act
        var dependencies = new MessageDependencies(
            typeof(StubMessages.StubNonGenericMessage),
            descriptor,
            serviceProvider);

        // Assert: should be StubGenericHandler<string>
        Assert.Equal(typeof(StubHandlers.StubNonGenericHandler), dependencies.Handlers.First().Descriptor.HandlerType);
        Assert.Equal(typeof(StubHandlers.StubNonGenericPreInterceptor), dependencies.PreInterceptors.First().Descriptor.HandlerType);
        Assert.Equal(typeof(StubHandlers.StubNonGenericPostInterceptor), dependencies.PostInterceptors.First().Descriptor.HandlerType);
        Assert.Equal(typeof(StubHandlers.StubNonGenericExceptionInterceptor), dependencies.ExceptionInterceptors.First().Descriptor.HandlerType);

    }   
    
    
    [Fact]
    public void MessageDependenciesShouldResolveHandlerInstance()
    {
        // Arrange
        var serviceProvider = new Mock<IServiceProvider>();

        var messageType = typeof(StubMessages.StubGenericMessage<string>);
        var handlerType = typeof(StubHandlers.StubGenericHandler<string>);

        // register fake handler in service provider
        var handlerInstance = new StubHandlers.StubGenericHandler<string>();
        serviceProvider
            .Setup(sp => sp.GetService(handlerType))
            .Returns(handlerInstance);

        var handlerDescriptor =
            new HandlerDescriptorBuilderFactory()
                .BuildDescriptors(typeof(StubHandlers.StubGenericHandler<string>))
                .First();

        var messageDescriptor = new MessageDescriptor(messageType);
        messageDescriptor.AddDescriptor(handlerDescriptor);

        var deps = new MessageDependencies(
            messageType,
            messageDescriptor,
            serviceProvider.Object);

        // Act
        var resolvedHandler = deps.Handlers.First().Handler;

        // Assert
        Assert.NotNull( resolvedHandler);
    }
}