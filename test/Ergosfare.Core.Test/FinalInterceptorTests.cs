using System.Runtime.CompilerServices;
using Ergosfare.Context;
using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Handlers;
using Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Ergosfare.Core.Internal.Contexts;
using Ergosfare.Core.Test.__stubs__;
using Microsoft.Extensions.DependencyInjection;

namespace Ergosfare.Core.Test;

public class FinalInterceptorTests
{

    private record TestMessage : IMessage;
    private record TestIndirectMessage : TestMessage;
    private class TestHandler: IAsyncHandler<StubNonGenericMessage>
    {
        public Task HandleAsync(StubNonGenericMessage message, IExecutionContext context)
        {
            return Task.CompletedTask;
        }
    }
    private class TestFinalInterceptor: IAsyncFinalInterceptor<StubNonGenericMessage>
    {
        public static bool IsExecuted { get; private set; }
        
        public Task HandleAsync(StubNonGenericMessage message, object? result, Exception? exception, IExecutionContext context)
        {
            IsExecuted = true;
            return Task.CompletedTask;
        }
    }

    private class TestFinalInterceptor2 : IAsyncFinalInterceptor<StubNonGenericMessage, Task>
    {
        public static bool IsExecuted { get; private set; }
        public Task HandleAsync(StubNonGenericMessage message, Task? result, Exception? exception, IExecutionContext context)
        {
            IsExecuted = true;
            return Task.CompletedTask;
        }
    }

    [Fact]
    [Trait("Category", "Coverage")]
    [Trait("Category", "Unit")]
    public async Task  TestFinalInterceptorsShouldImplement()
    {
        
        var testInterceptor = new TestFinalInterceptor(); 
        var result = Task.CompletedTask; 
        var message = new StubNonGenericMessage(); 
        var exception = new Exception(); 
        var ct = CancellationToken.None; 
        var values = new Dictionary<object, object?>();
        var context = StubExecutionContext.Create();
        
        
        IFinalInterceptor interceptor1 = new TestFinalInterceptor(); 
        IFinalInterceptor interceptor2 = new TestFinalInterceptor2();
        IAsyncFinalInterceptor<StubNonGenericMessage> interceptor3 = new TestFinalInterceptor();
        
        
        await using (AmbientExecutionContext.CreateScope(context))
        {
            var result1 = interceptor1.Handle(message, result, exception, AmbientExecutionContext.Current); 
            Assert.NotNull(result1);
        }
     
        
        await using (AmbientExecutionContext.CreateScope(context))
        {
            var result2 = interceptor2.Handle(message, result, exception, AmbientExecutionContext.Current); 
            Assert.NotNull(result2);
        }

        
        await using (AmbientExecutionContext.CreateScope(context))
        {
            var result3 = interceptor3.Handle(message, result, exception, AmbientExecutionContext.Current); 
            Assert.NotNull(result3); 
           await Assert.IsType<Task>(result3, exactMatch: false);
        }
        
        
    }

    [Fact]
    [Trait("Category", "Coverage")]
    [Trait("Category", "Unit")]
    public async Task TestFinalInterceptorsShouldRun()
    {
        var services = new ServiceCollection()
            .AddErgosfare(opts => opts
                .AddCoreModule(b => b
                    .Register<TestHandler>()
                    .Register<TestFinalInterceptor>()))
            .BuildServiceProvider();

        var mediator = services.GetRequiredService<IMessageMediator>();
        
      
    }
    
    
    
    [Fact]
    [Trait("Category", "Coverage")]
    [Trait("Category", "Unit")]
    public async Task TestIndirectFinalInterceptorsShouldRun()
    {
        var services = new ServiceCollection()
            .AddErgosfare(opts => opts
                .AddCoreModule(b => b
                    .Register<TestHandler>()
                    .Register<TestFinalInterceptor>()))
            .BuildServiceProvider();

        var mediator = services.GetRequiredService<IMessageMediator>();
        
       
    }
}