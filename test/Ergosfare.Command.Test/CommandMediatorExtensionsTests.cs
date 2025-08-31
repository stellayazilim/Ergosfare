using Ergosfare.Command.Test.__stubs__;
using Ergosfare.Commands.Abstractions;
using Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Ergosfare.Context;
using Ergosfare.Contracts;
using Ergosfare.Core.Abstractions.Handlers;
using Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Ergosfare.Command.Test;

public class CommandMediatorExtensionsTests
{
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    [Fact]
    public async Task CommandMediatorExtensionsShouldExecuteCommandWithGroup()
    {
        var mockInterceptor1 = new Mock<StubCommandPreInterceptor1>();
        var mockInterceptor2 = new Mock<StubCommandPreInterceptor2>();



        var cancellationToken = CancellationToken.None;
        var cmd = new StubNonGenericCommand();
        mockInterceptor1.Setup(s =>
            s.HandleAsync(It.IsAny<StubNonGenericCommand>(), It.IsAny<IExecutionContext>(), It.IsAny<CancellationToken>())) .CallBase();;
        mockInterceptor2.Setup(s => 
            s.HandleAsync(It.IsAny<StubNonGenericCommand>(), It.IsAny<IExecutionContext>(), It.IsAny<CancellationToken>())) .CallBase();;
        
        
        var services = new ServiceCollection()
            .AddErgosfare(options =>
            {
                options.AddCommandModule(module =>
                {
                    module.Register(mockInterceptor2.Object.GetType());
                    module.Register(mockInterceptor1.Object.GetType());
                    module.Register<StubNonGenericCommand>();
                });
            }).BuildServiceProvider();


        var mediator = services.GetRequiredService<ICommandMediator>();
        
        
        await mediator.SendAsync(cmd, ["group2"], cancellationToken);
        
        Assert.True(StubNonGenericCommandHandler.HasCalled);
        Assert.False(StubCommandPreInterceptor1.HasCalled);
        Assert.True(StubCommandPreInterceptor2.HasCalled);
   
    }
    
    
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    [Fact]
    public async Task CommandMediatorExtensionsShouldExecuteCommandWithCancellationToken()
    {
 

        var cancellationToken = CancellationToken.None;
        var cmd = new StubNonGenericCommand();

        
        var services = new ServiceCollection()
            .AddErgosfare(options =>
            {
                options.AddCommandModule(module =>
                {
                    module.Register<StubNonGenericCommandHandler>();
                });
            }).BuildServiceProvider();


        var mediator = services.GetRequiredService<ICommandMediator>();
        
        
        await mediator.SendAsync(cmd,  cancellationToken);
        
        Assert.True(StubNonGenericCommandHandler.HasCalled);
   
    }
    
    
    
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    [Fact]
    public async Task CommandMediatorExtensionsShouldExecuteCommandWithResultCancellationToken()
    {
 

        var cancellationToken = CancellationToken.None;
        var cmd = new StubNonGenericCommandStringResult();

        
        var services = new ServiceCollection()
            .AddErgosfare(options =>
            {
                options.AddCommandModule(module =>
                {
                    module.Register<StubNonGenericCommandStringResultHandler>();
                });
            }).BuildServiceProvider();


        var mediator = services.GetRequiredService<ICommandMediator>();
        
        
        var result = await mediator.SendAsync(cmd,  cancellationToken);
        
        Assert.True(StubNonGenericCommandStringResultHandler.HasCalled);
        Assert.Equal(string.Empty, result);
    }
    
    
    
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    [Fact]
    public async Task CommandMediatorExtensionsShouldExecuteCommandWithResultGroup()
    {
 

        var cancellationToken = CancellationToken.None;
        var cmd = new StubNonGenericCommandStringResult();

        
        var services = new ServiceCollection()
            .AddErgosfare(options =>
            {
                options.AddCommandModule(module =>
                {
                    module.Register<StubNonGenericCommandStringResultHandler>();
                });
            }).BuildServiceProvider();


        var mediator = services.GetRequiredService<ICommandMediator>();
        
        
        var result = await mediator.SendAsync(cmd,["default"],  cancellationToken);
        
        Assert.True(StubNonGenericCommandStringResultHandler.HasCalled);
        Assert.Equal(string.Empty, result);
    }
}