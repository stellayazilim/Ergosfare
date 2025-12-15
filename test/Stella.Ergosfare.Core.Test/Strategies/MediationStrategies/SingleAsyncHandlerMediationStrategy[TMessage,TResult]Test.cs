// using Stella.Ergosfare.Context;
// using Stella.Stella.Ergosfare.Contracts.Attributes;
// using Stella.Stella.Stella.Ergosfare.Core.Abstractions;
// using Stella.Stella.Stella.Ergosfare.Core.Abstractions.Exceptions;
// using Stella.Stella.Stella.Ergosfare.Core.Abstractions.Handlers;
// using Stella.Stella.Stella.Ergosfare.Core.Abstractions.Strategies;
// using Stella.Stella.Ergosfare.Core.Internal.Factories;
// using Stella.Stella.Ergosfare.Core.Internal.Registry;
// using Stella.Stella.Ergosfare.Core.Internal.Registry.Descriptors;
// using Stella.Stella.Stella.Ergosfare.Core.Test.__stubs__;
// using Stella.Stella.Stella.Ergosfare.Core.Test.__stubs__.Handlers;
// using Stella.Stella.Stella.Ergosfare.Core.Test.Common;
// using Stella.Stella.Stella.Ergosfare.Test.Fixtures;
// using Microsoft.Extensions.DependencyInjection;
// using Xunit.Abstractions;
//
// namespace Stella.Stella.Stella.Ergosfare.Core.Test.Strategies;
//
// public class SingleAsyncHandlerMediationStrategyTests: 
//     IClassFixture<IFixture<ExecutionContextFixture>>,
//     IClassFixture<IFixture<MessageDependencyFixture>>
// {
//     private readonly IFixture<MessageDependencyFixture> _messageDependencyFixture;
//     private readonly IFixture<ExecutionContextFixture> _executionContextFixture;
//     public SingleAsyncHandlerMediationStrategyTests(
//         ITestOutputHelper testOutputHelper, 
//         IFixture<MessageDependencyFixture>  messageDependencyFixture,
//         IFixture<ExecutionContextFixture> executionContextFixture) 
//     {
//         _messageDependencyFixture = messageDependencyFixture;
//         _executionContextFixture = executionContextFixture;
//     }
//     
//     [Fact]
//     [Trait("Category", "Coverage")]
//     [Trait("Strategy", "Unit")]
//     public async Task SingleAsyncHandlerMediationStrategyTMessageTResult_ThrowArgumentNullException()
//     {
//         var dependencyFixture = _messageDependencyFixture.New;
//         
//         
//         var messageDescriptor = new MessageDescriptor(typeof(TestExceptionMessage));
//
//         var strategy = new SingleAsyncHandlerMediationStrategy<TestExceptionMessage, string>(new ResultAdapterService());
//         
//         
//         await using( var  _ = AmbientExecutionContext
//                   .CreateScope(StubExecutionContext.Create()))
//              
//         {
//
//             await Assert.ThrowsAsync<ArgumentNullException>(() =>
//                 strategy.Mediate(new TestExceptionMessage(false, string.Empty), null, AmbientExecutionContext.Current));
//         }
//     }
//     
//     
//     
//     [Fact]
//     [Trait("Category", "Coverage")]
//     [Trait("Strategy", "Unit")]
//     public async Task SingleAsyncHandlerMediationStrategyTMessageTResult_ThrowMultipleHandlerException()
//     {
//
//         // arrange
//         var serviceProvider = new ServiceCollection()
//             .AddTransient<IResultAdapterService, ResultAdapterService>()
//             .AddTransient<TestExceptionMessageHandler>()
//             .AddTransient<TestExceptionMessageDuplicateHandler>()
//             .BuildServiceProvider();
//         
//         var registry = new MessageRegistry(
//                 new HandlerDescriptorBuilderFactory()
//             );
//         
//         registry.Register(typeof(TestExceptionMessageHandler));
//         registry.Register(typeof(TestExceptionMessageDuplicateHandler));
//
//         var resolver = new ActualTypeOrFirstAssignableTypeMessageResolveStrategy(registry);
//         var strategy = new SingleAsyncHandlerMediationStrategy<TestExceptionMessage, string>(serviceProvider.GetRequiredService<IResultAdapterService>());
//         
//         var descriptor = resolver.Find(typeof(TestExceptionMessage));
//         var dependencies = new MessageDependenciesFactory(serviceProvider)
//             .Create(typeof(TestExceptionMessage), descriptor!,[]);
//         
//         // act
//         await using( var  _ = AmbientExecutionContext
//                         .CreateScope(StubExecutionContext.Create()))
//         {
//             // assert
//             await Assert.ThrowsAsync<MultipleHandlerFoundException>(() =>
//                 strategy.Mediate(new TestExceptionMessage(false, string.Empty), dependencies, AmbientExecutionContext.Current));
//         }
//         
//     }
//     
//     
//     
//         
//     [Fact]
//     [Trait("Category", "Coverage")]
//     [Trait("Strategy", "Unit")]
//     public async Task SingleAsyncHandlerMediationStrategyTMessageTResult_ShoulRunPipeline()
//     {
//
//         // arrange
//         var serviceProvider = new ServiceCollection()
//             .AddTransient<IResultAdapterService, ResultAdapterService>()
//             .AddTransient<TestExceptionMessageHandler>()
//             .AddTransient<TestExceptionMessagePreInterceptor>()
//             .AddTransient<TestExceptionMessagePostInterceptor>()
//             .AddTransient<TestExceptionMessageExceptionInterceptor>()
//             .BuildServiceProvider();
//         
//         var registry = new MessageRegistry(
//             new HandlerDescriptorBuilderFactory()
//         );
//         
//         registry.Register(typeof(TestExceptionMessageHandler));
//         registry.Register(typeof(TestExceptionMessagePreInterceptor));
//         registry.Register(typeof(TestExceptionMessagePostInterceptor));
//         registry.Register(typeof(TestExceptionMessageExceptionInterceptor));
//
//         var resolver = new ActualTypeOrFirstAssignableTypeMessageResolveStrategy(registry);
//         var strategy = new SingleAsyncHandlerMediationStrategy<TestExceptionMessage, string>(serviceProvider.GetRequiredService<IResultAdapterService>());
//         
//         var descriptor = resolver.Find(typeof(TestExceptionMessage));
//         var dependencies = new MessageDependenciesFactory(serviceProvider)
//             .Create(typeof(TestExceptionMessage), descriptor!, []);
//         
//      
//         await using( var  _ = AmbientExecutionContext
//                         .CreateScope( StubExecutionContext.Create()))
//                 
//         {
//             // act
//             var result = await strategy.Mediate(new TestExceptionMessage(false, "test"), dependencies, AmbientExecutionContext.Current);
//             
//             Assert.Equal("test",result);
//             
//         }
//         
//     }
//     
//
//     
//     [Fact]
//     [Trait("Category", "Coverage")]
//     [Trait("Strategy", "Unit")]
//     public async Task SingleAsyncHandlerMediationStrategy_ShouldThrowInvalidOperationException_WhenHandlerResolvesToNull()
//     {
//
//         // Arrange
//         
//         var dependencies = new StubMessageDependencies();
//         var strategy = new SingleAsyncHandlerMediationStrategy<StubNonGenericMessage, string>(new ResultAdapterService());
//
//      
//         await using (var _ = AmbientExecutionContext.CreateScope( StubExecutionContext.Create() ))
//         {
//             // act
//             var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
//                 strategy.Mediate(
//                     new StubNonGenericMessage(), 
//                     dependencies, 
//                     AmbientExecutionContext.Current));
//             
//             // Assert
//             Assert.Equal(
//                    $"Handler for {nameof(StubNonGenericMessage)} is not of the expected type."
//                , ex.Message);
//         }
//     }
//
//     
//     
//     [Fact]
//     [Trait("Category", "Coverage")]
//     public async Task SingleAsyncHandlerMediationStrategy_ShouldThrowWhenExecutionAbortedWithResultValueNull()
//     {
//         // arrange
//         var serviceProvider = new ServiceCollection()
//             .AddTransient<IResultAdapterService, ResultAdapterService>()
//             .AddTransient<StubNonGenericStringResultHandler>()
//             .AddTransient<TestExceptionAborterPreInterceptor>()
//             .BuildServiceProvider();
//
//
//         var messageRegistry = new MessageRegistry(new HandlerDescriptorBuilderFactory());
//         
//         messageRegistry.Register(typeof(StubNonGenericMessage));
//         messageRegistry.Register(typeof(StubNonGenericStringResultHandler));
//         messageRegistry.Register(typeof(TestExceptionAborterPreInterceptor));
//         
//         var  resolver = new ActualTypeOrFirstAssignableTypeMessageResolveStrategy(messageRegistry);
//
//         var descriptor = resolver.Find(typeof(StubNonGenericMessage));
//
//         var dependencies = new MessageDependenciesFactory(serviceProvider).Create(
//             typeof(StubNonGenericMessage), descriptor!, []);
//         
//         var mediationStrategy = new SingleAsyncHandlerMediationStrategy<StubNonGenericMessage, string>(serviceProvider.GetRequiredService<IResultAdapterService>());
//
//
//         await using var _ = AmbientExecutionContext.CreateScope(
//             StubExecutionContext.Create()
//         );
//
//         // Assert that Abort throws
//         var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => mediationStrategy.Mediate(
//                 new StubNonGenericMessage(),
//                 dependencies,
//                 AmbientExecutionContext.Current));
//
//         
//         Assert.Equal($"A Message result of type '{nameof(String)}' is required when the execution is aborted as this message has a specific result.", ex.Message);
//     }
//     
//     
//     
//     
//       
//     [Fact]
//     [Trait("Category", "Coverage")]
//     public async Task SingleAsyncHandlerMediationStrategy_ShouldThrowWhenExecutionAbortedWithResultValue()
//     {
//         // arrange
//         var serviceProvider = new ServiceCollection()
//             .AddTransient<StubNonGenericStringResultHandler>()
//             .AddTransient<TestExceptionAborterResultPreInterceptor>()
//             .BuildServiceProvider();
//
//
//         var messageRegistry = new MessageRegistry(new HandlerDescriptorBuilderFactory());
//         
//         messageRegistry.Register(typeof(StubNonGenericMessage));
//         messageRegistry.Register(typeof(StubNonGenericStringResultHandler));
//         messageRegistry.Register(typeof(TestExceptionAborterResultPreInterceptor));
//         
//         var  resolver = new ActualTypeOrFirstAssignableTypeMessageResolveStrategy(messageRegistry);
//
//         var descriptor = resolver.Find(typeof(StubNonGenericMessage));
//
//         var dependencies = new MessageDependenciesFactory(serviceProvider).Create(
//             typeof(StubNonGenericMessage), descriptor!, []);
//         
//         var mediationStrategy = new SingleAsyncHandlerMediationStrategy<StubNonGenericMessage, string>(new ResultAdapterService());
//
//
//         await using var _ = AmbientExecutionContext.CreateScope(
//             StubExecutionContext.Create()
//         );
//
//         // Assert that Abort throws
//         var result = await mediationStrategy.Mediate(
//             new StubNonGenericMessage(),
//             dependencies,
//             AmbientExecutionContext.Current);
//
//         
//         Assert.Equal("foo", result);
//     }
//     
//     
//     
//     [Fact]
//     [Trait("Category", "Coverage")]
//     public async Task SingleAsyncHandlerMediationStrategy_ShouldThrowWhenCatchUnknownException()
//     {
//         var serviceProvider = new ServiceCollection()
//             .AddTransient<IResultAdapterService, ResultAdapterService>()
//             .AddTransient<StubNonGenericStringResultHandler>()
//             .AddTransient<TestExceptionAborterResultPreInterceptor>()
//             .BuildServiceProvider();
//
//
//         var messageRegistry = new MessageRegistry(new HandlerDescriptorBuilderFactory());
//         
//         messageRegistry.Register(typeof(StubNonGenericMessage));
//         messageRegistry.Register(typeof(StubNonGenericStringResultHandler));
//         messageRegistry.Register(typeof(TestExceptionAborterResultPreInterceptor));
//         
//         var  resolver = new ActualTypeOrFirstAssignableTypeMessageResolveStrategy(messageRegistry);
//
//         var descriptor = resolver.Find(typeof(StubNonGenericMessage));
//
//         var dependencies = new MessageDependenciesFactory(serviceProvider).Create(
//             typeof(StubNonGenericMessage), descriptor!, []);
//         
//         var mediationStrategy = new SingleAsyncHandlerMediationStrategy<StubNonGenericMessage, string>(serviceProvider.GetRequiredService<IResultAdapterService>());
//
//
//         await using var _ = AmbientExecutionContext.CreateScope(
//             StubExecutionContext.Create()
//         );
//
//         // Assert that Abort throws
//         var result = await mediationStrategy.Mediate(
//             new StubNonGenericDerivedMessage(),
//             dependencies,
//             AmbientExecutionContext.Current);
//
//         
//         Assert.Equal("foo", result);
//     }
//     
//     
//     [Fact]
//     [Trait("Category", "Coverage")]
//     public async Task Mediate_ShouldApplyModifiedResultFromExceptionInterceptor()
//     {   
//         // arrange
//         var message = "Test Message";
//         var (_, _, dependencies) = MessageDependencyFixture.CreateMessageDependencies<StubStringMessage>(
//             [GroupAttribute.DefaultGroupName],
//             typeof(StubStringMessageHandler),
//             typeof(StubStringMessagePreInterceptorThrows),
//             typeof(StubStringMessageExceptionInterceptorModifyResult));
//         var ctx = ExecutionContextFixture.CreateExecutionContext();
//         var strategy = new SingleAsyncHandlerMediationStrategy<StubStringMessage, string>(new ResultAdapterService());
//
//         // act
//         var result = await strategy.Mediate(new StubStringMessage(message), dependencies, ctx);
//         Assert.Equal("modified result", result);
//     }
// }