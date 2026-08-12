using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Internal.Factories;
using Stella.Ergosfare.Core.Internal.Mediator;
using Stella.Ergosfare.Test.Fixtures;
using Stella.Ergosfare.Test.Fixtures.Stubs.Basic;
using Stella.Ergosfare.Test.Fixtures.Stubs.Generic;
using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Core.Abstractions.Attributes;
// ReSharper disable ConvertToPrimaryConstructor

namespace Stella.Ergosfare.Core.Test;


/// <summary>
/// Contains unit tests for <see cref="MessageDependencies"/>,
/// <see cref="MessageDependenciesFactory"/>, and related message handler resolution.
/// </summary>
/// <remarks>
/// Tests cover creation of message dependencies, resolution of generic and indirect handlers,
/// and correct registration of pre/post/exception/final interceptors. The pipeline under
/// test is stated as a message type plus the participants serving it;
/// <see cref="FrozenCompositionBridge"/> turns that into the composition the dispatch path
/// reads, exactly as a compilation would.
/// </remarks>
public class MessageDependenciesTest:
    IClassFixture<MessageDependencyFixture>
{
    private MessageDependencyFixture _messageDependencyFixture;


    /// <summary>
    /// Initializes a new instance of the <see cref="MessageDependenciesTest"/> class.
    /// </summary>
    /// <param name="messageDependencyFixture">The message dependency fixture.</param>
    public MessageDependenciesTest(
        MessageDependencyFixture messageDependencyFixture)
    {
        _messageDependencyFixture = messageDependencyFixture;
    }

    /// <summary>
    /// The dependencies a composition of <paramref name="participantTypes"/> produces for
    /// <paramref name="messageType"/> under the given groups.
    /// </summary>
    private static MessageDependencies Dependencies(
        Type messageType, IServiceProvider serviceProvider, string[] groups, params Type[] participantTypes)
        => new(
            FrozenCompositionBridge.FromTypes(messageType, participantTypes).BuildShape(messageType, groups),
            serviceProvider);

    /// <summary>
    /// Tests that <see cref="MessageDependenciesFactory"/> creates message dependencies correctly.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void MessageDependenciesFactoryShouldCreateMessageDependencies()
    {
        _messageDependencyFixture = _messageDependencyFixture.New;
        _messageDependencyFixture.AddServices(sp => sp.AddTransient<StubVoidHandler>());
        _messageDependencyFixture.RegisterHandler(typeof(StubVoidHandler));
        // act: the factory reads the message's composition from the container's catalog,
        // which the fixture composes from the registered participants.
        var dependencies = _messageDependencyFixture.CreateDependencies<StubMessage>();
        // assert
        Assert.NotNull(dependencies);
        // cleanup
        _messageDependencyFixture.Dispose();
    }

    /// <summary>
    /// Tests that <see cref="MessageDependenciesFactory"/> creates message dependencies correctly.
    /// </summary>
    [Fact]
    [Trait("Category", "Coverage")]
    public void MessageDependenciesShouldGetIndirectHandlers()
    {
        // arrange
        var messgeDependencies = Dependencies(typeof(IMessage), null!, []);
        // act
        var indirectHandlers = messgeDependencies.IndirectHandlers;
        // assert
        Assert.NotNull(indirectHandlers);
        Assert.Empty(indirectHandlers);
        _messageDependencyFixture.Dispose();
    }


    /// <summary>
    /// Tests that generic message dependencies resolve handler and interceptor types correctly.
    /// </summary>
     [Fact]
     [Trait("Category", "Coverage")]
     public void MessageDependenciesShouldGetHandlerTypeMakeGeneric()
     {
        // Arrange
        var serviceProvider = new ServiceCollection()
            .AddTransient<VoidStubGenericHandler<string>>()
            .AddTransient<VoidStubGenericPreInterceptor<string>>()
            .AddTransient<VoidStubGenericPostInterceptor<string>>()
            .AddTransient<VoidStubGenericExceptionInterceptor<string>>()
            .AddTransient<VoidStubGenericFinalInterceptor<string>>()
            .BuildServiceProvider();

        // The participants are open definitions declared over the open message; the
        // composition closes them over the runtime message's arguments.
        var dependencies = Dependencies(
            typeof(StubGenericMessage<string>), serviceProvider, [GroupAttribute.DefaultGroupName],
            typeof(VoidStubGenericHandler<>),
            typeof(VoidStubGenericPreInterceptor<>),
            typeof(VoidStubGenericPostInterceptor<>),
            typeof(VoidStubGenericExceptionInterceptor<>),
            typeof(VoidStubGenericFinalInterceptor<>));

        // Assert: should be StubGenericHandler<string>
        Assert.Equal(typeof(VoidStubGenericHandler<string>), dependencies.Handlers[0].Resolve(serviceProvider).GetType());
        Assert.Equal(typeof(VoidStubGenericPreInterceptor<string>), dependencies.PreInterceptors[0].Resolve(serviceProvider).GetType());
        Assert.Equal(typeof(VoidStubGenericPostInterceptor<string>), dependencies.PostInterceptors[0].Resolve(serviceProvider).GetType());
        Assert.Equal(typeof(VoidStubGenericExceptionInterceptor<string>),  dependencies.ExceptionInterceptors[0].Resolve(serviceProvider).GetType());
        Assert.Equal(typeof(VoidStubGenericFinalInterceptor<string>),  dependencies.FinalInterceptors[0].Resolve(serviceProvider).GetType());
     }

    /// <summary>
    /// Tests that indirect message dependencies resolve handler types correctly.
    /// </summary>
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

         // Every participant is declared over StubMessage while the message dispatched is
         // its subtype — so each matches covariantly.
         var messageType = typeof(StubIndirectMessage);

         var dependencies = Dependencies(
             messageType, serviceProvider, [GroupAttribute.DefaultGroupName],
             typeof(StubVoidHandler),
             typeof(StubPreInterceptor),
             typeof(StubPostInterceptor),
             typeof(StubExceptionInterceptor),
             typeof(StubFinalInterceptor));

         Assert.True(messageType.IsAssignableTo(typeof(StubMessage)));
         Assert.Equal(typeof(StubVoidHandler), dependencies.IndirectHandlers[0].Resolve(serviceProvider).GetType());
         // Indirect interceptors are merged into the single per-stage lists.
         Assert.Equal(typeof(StubPreInterceptor), dependencies.PreInterceptors[0].Resolve(serviceProvider).GetType());
         Assert.Equal(typeof(StubPostInterceptor), dependencies.PostInterceptors[0].Resolve(serviceProvider).GetType());
         Assert.Equal(typeof(StubExceptionInterceptor), dependencies.ExceptionInterceptors[0].Resolve(serviceProvider).GetType());
         Assert.Equal(typeof(StubFinalInterceptor), dependencies.FinalInterceptors[0].Resolve(serviceProvider).GetType());
     }

    /// <summary>
    /// Tests that message dependencies resolve handler types correctly.
    /// </summary>
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

        // Act
        var dependencies = Dependencies(
            typeof(StubMessage), serviceProvider, [GroupAttribute.DefaultGroupName],
            typeof(StubVoidHandler),
            typeof(StubPreInterceptor),
            typeof(StubPostInterceptor),
            typeof(StubExceptionInterceptor),
            typeof(StubFinalInterceptor));

        // Assert
        Assert.Equal(typeof(StubVoidHandler), dependencies.Handlers.First().HandlerType);
        Assert.Equal(typeof(StubPreInterceptor), dependencies.PreInterceptors.First().HandlerType);
        Assert.Equal(typeof(StubPostInterceptor), dependencies.PostInterceptors.First().HandlerType);
        Assert.Equal(typeof(StubExceptionInterceptor), dependencies.ExceptionInterceptors.First().HandlerType);
        Assert.Equal(typeof(StubFinalInterceptor), dependencies.FinalInterceptors.First().HandlerType);
    }


     [Fact]
     public void MessageDependenciesShouldResolveHandlerInstance()
     {
         // Arrange
         var serviceProvider = new ServiceCollection()
             .AddTransient(typeof(VoidStubGenericHandler<>))
             .BuildServiceProvider();
         var messageType = typeof(StubGenericMessage<string>);

         var deps = Dependencies(
             messageType, serviceProvider, [], typeof(VoidStubGenericHandler<>));

         // Act
         var resolvedHandler = deps.Handlers[0].Resolve(serviceProvider);
         // Assert
         Assert.NotNull( resolvedHandler);
     }
}
