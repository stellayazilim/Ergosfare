using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Registry;
using Ergosfare.Core.Abstractions.Strategies;
using Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
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
         
         var services = new ServiceCollection()
             .AddErgosfare(options => options.AddCoreModule(b => b
                 .Register<VoidStubGenericHandler<string>>()))
             .BuildServiceProvider();

         var mediator = services.GetRequiredService<IMessageMediator>();

         await mediator.Mediate(new StubGenericMessage<string>(), new MediateOptions<StubGenericMessage<string>, Task>()
         {
             Groups = ["default"],
             Items = new Dictionary<object,object?>(),
             MessageMediationStrategy = new SingleAsyncHandlerMediationStrategy<StubGenericMessage<string>, Task>(null),
             MessageResolveStrategy = new ActualTypeOrFirstAssignableTypeMessageResolveStrategy(services.GetRequiredService<IMessageRegistry>()),
             CancellationToken = CancellationToken.None
         });
     }
     
//     
//     [Fact]
//     [Trait("Category", "Coverage")]
//     public void MessageDependenciesShouldGetIndirectHandlerType()
//     {
//         // Arrange
//         var serviceProvider = new ServiceCollection()
//             .AddTransient<StubNonGenericHandler>()
//             .AddTransient<StubNonGenericPreInterceptor>()
//             .AddTransient<StubNonGenericPostInterceptor>()
//             .AddTransient<StubNonGenericExceptionInterceptor>()
//             .AddTransient<StubNonGenericFinalInterceptor>()
//             .BuildServiceProvider();
//
//         // our generic message type to resolve against
//
//
//         var messageType = typeof(StubNonGenericDerivedMessage);
//         var indirectMessageType = typeof(StubNonGenericMessage);
//         // build descriptor manually
//         var handlerDescriptor = new MainHandlerDescriptor()
//         {
//             Weight = 1,
//             Groups = [GroupAttribute.DefaultGroupName],
//             MessageType = indirectMessageType,
//             HandlerType = typeof(StubNonGenericHandler),
//             ResultType = typeof(Task)
//             
//         }; 
//         
//         var preInterceptorDescriptor = new PreInterceptorDescriptor()
//         {
//             Weight = 1,
//             Groups = [GroupAttribute.DefaultGroupName],
//             MessageType = indirectMessageType,
//             HandlerType = typeof(StubNonGenericPreInterceptor),
//         }; 
//
//
//         var postInterceptorDescriptor = new PostInterceptorDescriptor()
//         {
//             Weight = 1,
//             Groups = [GroupAttribute.DefaultGroupName],
//             MessageType = indirectMessageType,
//             HandlerType = typeof(StubNonGenericPostInterceptor),
//             ResultType = typeof(Task)
//         };
//
//
//         var exceptionInterceptorDescriptor = new ExceptionInterceptorDescriptor()
//         {
//             Weight = 1,
//             Groups = [GroupAttribute.DefaultGroupName],
//             MessageType = indirectMessageType,
//             HandlerType = typeof(StubNonGenericExceptionInterceptor),
//             ResultType = typeof(Task)
//         };
//
//         var finalInterceptorDescriptor = new FinalInterceptorDescriptor()
//         {
//             Weight = 1,
//             Groups = [GroupAttribute.DefaultGroupName],
//             MessageType = indirectMessageType,
//             HandlerType = typeof(StubNonGenericFinalInterceptor),
//             ResultType = typeof(Task)
//         };
//         
//         var messageDescriptor = new MessageDescriptor(messageType);
//         messageDescriptor.AddDescriptor(handlerDescriptor);
//         messageDescriptor.AddDescriptor(preInterceptorDescriptor);
//         messageDescriptor.AddDescriptor(postInterceptorDescriptor);
//         messageDescriptor.AddDescriptor(exceptionInterceptorDescriptor);
//         messageDescriptor.AddDescriptor(finalInterceptorDescriptor);
//         
//         var dependencies = new MessageDependencies(
//             messageType,
//             messageDescriptor,
//             serviceProvider,
//             [GroupAttribute.DefaultGroupName]);
//
//         Assert.True(messageType.IsAssignableTo(handlerDescriptor.MessageType));
//         Assert.Equal(typeof(StubNonGenericDerivedMessage),messageDescriptor.MessageType);
//         // Assert: should be StubGenericHandler<string>
//         Assert.Equal(typeof(StubNonGenericHandler), dependencies.IndirectHandlers.First().Handler.Value.GetType());
//         Assert.Equal(typeof(StubNonGenericPreInterceptor), dependencies.IndirectPreInterceptors.First().Handler.Value.GetType());
//         Assert.Equal(typeof(StubNonGenericPostInterceptor), dependencies.IndirectPostInterceptors.First().Handler.Value.GetType());
//         Assert.Equal(typeof(StubNonGenericExceptionInterceptor), dependencies.IndirectExceptionInterceptors.First().Handler.Value.GetType());
//         Assert.Equal(typeof(StubNonGenericFinalInterceptor), dependencies.IndirectFinalInterceptors.First().Handler.Value.GetType());
//     }
//     
//     [Fact]
//     [Trait("Category", "Coverage")]
//     public void MessageDependenciesShouldResolveHandlersWithHandlerType()
//     {
//         // Arrange
//         var serviceProvider = new ServiceCollection()
//             .AddTransient<StubNonGenericHandler>()
//             .AddTransient<StubNonGenericPreInterceptor>()
//             .AddTransient<StubNonGenericPostInterceptor>()
//             .AddTransient<StubNonGenericExceptionInterceptor>()
//             .AddTransient<StubNonGenericFinalInterceptor>()
//             .BuildServiceProvider();
//         var descriptorFactory = new HandlerDescriptorBuilderFactory();
//
//         var descriptor = new MessageDescriptor(typeof(StubNonGenericMessage));
//
//         var mainHandlerDescriptor = descriptorFactory.BuildDescriptors(
//                 typeof(StubNonGenericHandler)
//             );
//
//         var preHandlerDescriptor = descriptorFactory.BuildDescriptors(
//             typeof(StubNonGenericPreInterceptor)
//         );
//
//         var postHandlerDescriptor = descriptorFactory.BuildDescriptors(
//             typeof(StubNonGenericPostInterceptor)
//             );
//
//         var exceptionHandlerDescriptor = descriptorFactory.BuildDescriptors(
//             typeof(StubNonGenericExceptionInterceptor)
//             );
//
//         var finalInterceptorDescriptor = descriptorFactory.BuildDescriptors(
//             typeof(StubNonGenericFinalInterceptor));
//         descriptor.AddDescriptors(mainHandlerDescriptor);
//         descriptor.AddDescriptors(preHandlerDescriptor);
//         descriptor.AddDescriptors(postHandlerDescriptor);
//         descriptor.AddDescriptors(exceptionHandlerDescriptor);
//         descriptor.AddDescriptors(finalInterceptorDescriptor);
//         // Act
//         var dependencies = new MessageDependencies(
//             typeof(StubNonGenericMessage),
//             descriptor,
//             serviceProvider, [GroupAttribute.DefaultGroupName]);
//
//         // Assert: should be StubGenericHandler<string>
//         Assert.Equal(typeof(StubNonGenericHandler), dependencies.Handlers.First().Descriptor.HandlerType);
//         Assert.Equal(typeof(StubNonGenericPreInterceptor), dependencies.PreInterceptors.First().Descriptor.HandlerType);
//         Assert.Equal(typeof(StubNonGenericPostInterceptor), dependencies.PostInterceptors.First().Descriptor.HandlerType);
//         Assert.Equal(typeof(StubNonGenericExceptionInterceptor), dependencies.ExceptionInterceptors.First().Descriptor.HandlerType);
// //        Assert.Equal(typeof(StubNonGenericFinalInterceptor), dependencies.FinalInterceptors.First().Descriptor.HandlerType);
//     }   
//     
//     
//     [Fact]
//     public void MessageDependenciesShouldResolveHandlerInstance()
//     {
//         // Arrange
//         var serviceProvider = new Mock<IServiceProvider>();
//
//         var messageType = typeof(StubGenericMessage<string>);
//         var handlerType = typeof(StubGenericHandler<string>);
//
//         // register fake handler in service provider
//         var handlerInstance = new StubGenericHandler<string>();
//         serviceProvider
//             .Setup(sp => sp.GetService(handlerType))
//             .Returns(handlerInstance);
//
//         var handlerDescriptor =
//             new HandlerDescriptorBuilderFactory()
//                 .BuildDescriptors(typeof(StubGenericHandler<string>))
//                 .First();
//
//         var messageDescriptor = new MessageDescriptor(messageType);
//         messageDescriptor.AddDescriptor(handlerDescriptor);
//
//         var deps = new MessageDependencies(
//             messageType,
//             messageDescriptor,
//             serviceProvider.Object, []);
//
//         // Act
//         var resolvedHandler = deps.Handlers.First().Handler;
//
//         // Assert
//         Assert.NotNull( resolvedHandler);
//     }
}