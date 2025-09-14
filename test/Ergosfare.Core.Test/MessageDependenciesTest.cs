using Ergosfare.Contracts.Attributes;
using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Internal.Builders;
using Ergosfare.Core.Internal.Factories;
using Ergosfare.Core.Internal.Mediator;
using Ergosfare.Core.Internal.Registry.Descriptors;
using Ergosfare.Core.Test.__stubs__;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit.Abstractions;

namespace Ergosfare.Core.Test;

public class MessageDependenciesTest
(ITestOutputHelper testOutputHelper)
{

    [Fact]
    [Trait("Category", "Unit")]
    public void MessageDependenciesFactoryShouldCreateMessageDependencies()
    {
        // arrange 
        var serviceCollection = new ServiceCollection().BuildServiceProvider();
        var descriptor = new MessageDescriptor(typeof(StubNonGenericMessage));
        var messageDependenciesFactory = new MessageDependenciesFactory(serviceCollection);

        
        
        // act
        var dependencies = messageDependenciesFactory.Create(typeof(StubNonGenericMessage), descriptor, []);
        
        // assert 
        Assert.NotNull(dependencies);
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
    }



    [Fact]
    [Trait("Category", "Coverage")]
    public void MessageDependenciesShouldGetHandlerTypeMakeGeneric()
    {
        // Arrange
        var serviceProvider = new ServiceCollection()
            .AddTransient<StubGenericHandler<string>>()
            .AddTransient<StubGenericPreInterceptor<string>>()
            .AddTransient<StubGenericPostInterceptor<string>>()
            .AddTransient<StubGenericExceptionInterceptor<string>>()
            .AddTransient<StubGenericFinalInterceptor<string>>()
            .BuildServiceProvider();

        // our generic message type to resolve against
       
      
        

        // build descriptor manually
        var handlerDescriptor = new MainHandlerDescriptor()
        {
            Weight = 1,
            Groups = [GroupAttribute.DefaultGroupName],
            MessageType = typeof(StubGenericMessage<>),
            HandlerType = typeof(StubGenericHandler<>),
            ResultType = typeof(Task)
            
        }; 
        
        var preInterceptorDescriptor = new PreInterceptorDescriptor()
        {
            Weight = 1,
            Groups = [GroupAttribute.DefaultGroupName],
            MessageType = typeof(StubGenericMessage<>),
            HandlerType = typeof(StubGenericPreInterceptor<>),
        }; 


        var postInterceptorDescriptor = new PostInterceptorDescriptor()
        {
            Weight = 1,
            Groups = [GroupAttribute.DefaultGroupName],
            MessageType = typeof(StubGenericMessage<>),
            HandlerType = typeof(StubGenericPostInterceptor<>),
            ResultType = typeof(Task)
        };


        var exceptionInterceptorDescriptor = new ExceptionInterceptorDescriptor()
        {
            Weight = 1,
            Groups = [GroupAttribute.DefaultGroupName],
            MessageType = typeof(StubGenericMessage<>),
            HandlerType = typeof(StubGenericExceptionInterceptor<>),
            ResultType = typeof(Task)
        };

        var finalInterceptorDescriptor = new FinalInterceptorDescriptor()
        {
            Weight = 1,
            Groups = [GroupAttribute.DefaultGroupName],
            MessageType = typeof(StubGenericMessage<>),
            HandlerType = typeof(StubGenericFinalInterceptor<>),
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
        Assert.Equal(typeof(StubGenericHandler<string>), dependencies.Handlers.First().Handler.Value.GetType());
        Assert.Equal(typeof(StubGenericPreInterceptor<string>), dependencies.PreInterceptors.First().Handler.Value.GetType());
        Assert.Equal(typeof(StubGenericPostInterceptor<string>), dependencies.PostInterceptors.First().Handler.Value.GetType());
        Assert.Equal(typeof(StubGenericExceptionInterceptor<string>),  dependencies.ExceptionInterceptors.First().Handler.Value.GetType());
        Assert.Equal(typeof(StubGenericFinalInterceptor<string>),  dependencies.FinalInterceptors.First().Handler.Value.GetType());
    }
    
    
    [Fact]
    [Trait("Category", "Coverage")]
    public void MessageDependenciesShouldGetIndirectHandlerType()
    {
        // Arrange
        var serviceProvider = new ServiceCollection()
            .AddTransient<StubNonGenericHandler>()
            .AddTransient<StubNonGenericPreInterceptor>()
            .AddTransient<StubNonGenericPostInterceptor>()
            .AddTransient<StubNonGenericExceptionInterceptor>()
            .AddTransient<StubNonGenericFinalInterceptor>()
            .BuildServiceProvider();

        // our generic message type to resolve against


        var messageType = typeof(StubNonGenericDerivedMessage);
        var indirectMessageType = typeof(StubNonGenericMessage);
        // build descriptor manually
        var handlerDescriptor = new MainHandlerDescriptor()
        {
            Weight = 1,
            Groups = [GroupAttribute.DefaultGroupName],
            MessageType = indirectMessageType,
            HandlerType = typeof(StubNonGenericHandler),
            ResultType = typeof(Task)
            
        }; 
        
        var preInterceptorDescriptor = new PreInterceptorDescriptor()
        {
            Weight = 1,
            Groups = [GroupAttribute.DefaultGroupName],
            MessageType = indirectMessageType,
            HandlerType = typeof(StubNonGenericPreInterceptor),
        }; 


        var postInterceptorDescriptor = new PostInterceptorDescriptor()
        {
            Weight = 1,
            Groups = [GroupAttribute.DefaultGroupName],
            MessageType = indirectMessageType,
            HandlerType = typeof(StubNonGenericPostInterceptor),
            ResultType = typeof(Task)
        };


        var exceptionInterceptorDescriptor = new ExceptionInterceptorDescriptor()
        {
            Weight = 1,
            Groups = [GroupAttribute.DefaultGroupName],
            MessageType = indirectMessageType,
            HandlerType = typeof(StubNonGenericExceptionInterceptor),
            ResultType = typeof(Task)
        };

        var finalInterceptorDescriptor = new FinalInterceptorDescriptor()
        {
            Weight = 1,
            Groups = [GroupAttribute.DefaultGroupName],
            MessageType = indirectMessageType,
            HandlerType = typeof(StubNonGenericFinalInterceptor),
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
        Assert.Equal(typeof(StubNonGenericDerivedMessage),messageDescriptor.MessageType);
        // Assert: should be StubGenericHandler<string>
        Assert.Equal(typeof(StubNonGenericHandler), dependencies.IndirectHandlers.First().Handler.Value.GetType());
        Assert.Equal(typeof(StubNonGenericPreInterceptor), dependencies.IndirectPreInterceptors.First().Handler.Value.GetType());
        Assert.Equal(typeof(StubNonGenericPostInterceptor), dependencies.IndirectPostInterceptors.First().Handler.Value.GetType());
        Assert.Equal(typeof(StubNonGenericExceptionInterceptor), dependencies.IndirectExceptionInterceptors.First().Handler.Value.GetType());
        Assert.Equal(typeof(StubNonGenericFinalInterceptor), dependencies.IndirectFinalInterceptors.First().Handler.Value.GetType());
    }
    
    [Fact]
    [Trait("Category", "Coverage")]
    public void MessageDependenciesShouldResolveHandlersWithHandlerType()
    {
        // Arrange
        var serviceProvider = new ServiceCollection()
            .AddTransient<StubNonGenericHandler>()
            .AddTransient<StubNonGenericPreInterceptor>()
            .AddTransient<StubNonGenericPostInterceptor>()
            .AddTransient<StubNonGenericExceptionInterceptor>()
            .AddTransient<StubNonGenericFinalInterceptor>()
            .BuildServiceProvider();
        var descriptorFactory = new HandlerDescriptorBuilderFactory();

        var descriptor = new MessageDescriptor(typeof(StubNonGenericMessage));

        var mainHandlerDescriptor = descriptorFactory.BuildDescriptors(
                typeof(StubNonGenericHandler)
            );

        var preHandlerDescriptor = descriptorFactory.BuildDescriptors(
            typeof(StubNonGenericPreInterceptor)
        );

        var postHandlerDescriptor = descriptorFactory.BuildDescriptors(
            typeof(StubNonGenericPostInterceptor)
            );

        var exceptionHandlerDescriptor = descriptorFactory.BuildDescriptors(
            typeof(StubNonGenericExceptionInterceptor)
            );

        var finalInterceptorDescriptor = descriptorFactory.BuildDescriptors(
            typeof(StubNonGenericFinalInterceptor));
        descriptor.AddDescriptors(mainHandlerDescriptor);
        descriptor.AddDescriptors(preHandlerDescriptor);
        descriptor.AddDescriptors(postHandlerDescriptor);
        descriptor.AddDescriptors(exceptionHandlerDescriptor);
        descriptor.AddDescriptors(finalInterceptorDescriptor);
        // Act
        var dependencies = new MessageDependencies(
            typeof(StubNonGenericMessage),
            descriptor,
            serviceProvider, [GroupAttribute.DefaultGroupName]);

        // Assert: should be StubGenericHandler<string>
        Assert.Equal(typeof(StubNonGenericHandler), dependencies.Handlers.First().Descriptor.HandlerType);
        Assert.Equal(typeof(StubNonGenericPreInterceptor), dependencies.PreInterceptors.First().Descriptor.HandlerType);
        Assert.Equal(typeof(StubNonGenericPostInterceptor), dependencies.PostInterceptors.First().Descriptor.HandlerType);
        Assert.Equal(typeof(StubNonGenericExceptionInterceptor), dependencies.ExceptionInterceptors.First().Descriptor.HandlerType);
//        Assert.Equal(typeof(StubNonGenericFinalInterceptor), dependencies.FinalInterceptors.First().Descriptor.HandlerType);
    }   
    
    
    [Fact]
    public void MessageDependenciesShouldResolveHandlerInstance()
    {
        // Arrange
        var serviceProvider = new Mock<IServiceProvider>();

        var messageType = typeof(StubGenericMessage<string>);
        var handlerType = typeof(StubGenericHandler<string>);

        // register fake handler in service provider
        var handlerInstance = new StubGenericHandler<string>();
        serviceProvider
            .Setup(sp => sp.GetService(handlerType))
            .Returns(handlerInstance);

        var handlerDescriptor =
            new HandlerDescriptorBuilderFactory()
                .BuildDescriptors(typeof(StubGenericHandler<string>))
                .First();

        var messageDescriptor = new MessageDescriptor(messageType);
        messageDescriptor.AddDescriptor(handlerDescriptor);

        var deps = new MessageDependencies(
            messageType,
            messageDescriptor,
            serviceProvider.Object, []);

        // Act
        var resolvedHandler = deps.Handlers.First().Handler;

        // Assert
        Assert.NotNull( resolvedHandler);
    }
}