using Ergosfare.Command.Test.__stubs__;
using Ergosfare.Commands;
using Ergosfare.Commands.Abstractions;
using Ergosfare.Core;
using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.EventHub;
using Ergosfare.Core.Abstractions.Strategies;
using Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Ergosfare.Command.Test;

public class CommandMediatorTests
{
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void ShouldResolveCommandTCommand()
    {

        var serviceCollection = new ServiceCollection()
            .AddErgosfare(options =>
                {
                    options.AddCoreModule(x =>
                    {
                        x.Register<StubNonGenericCommandHandler>();
                    });
                    
                }).BuildServiceProvider();
        
        var messageMediator = serviceCollection.GetService<IMessageMediator>();
        var mediator = new CommandMediator(
            new EventHub(),
            serviceCollection.GetRequiredService<ActualTypeOrFirstAssignableTypeMessageResolveStrategy>(),
            new ResultAdapterService(),
            messageMediator!);
        var result = mediator.SendAsync(new StubNonGenericCommand(), null,  CancellationToken.None);
        
        Assert.NotNull(result);
    }


    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ShouldResolveCommandTCommandTResult()
    {
        
        var serviceCollection = new ServiceCollection()
            .AddErgosfare(options =>
            {
                options.AddCoreModule(x =>
                {
                    x.Register<StubNonGenericCommandStringResultHandler>();
                });
                    
            }).BuildServiceProvider();
        
        var messageMediator = serviceCollection.GetRequiredService<IMessageMediator>();
        var mediator = new CommandMediator(
            new EventHub(),
            serviceCollection.GetRequiredService<ActualTypeOrFirstAssignableTypeMessageResolveStrategy>(),
            new ResultAdapterService(),
            messageMediator!);
        var result = mediator.SendAsync(new StubNonGenericCommandStringResult(), StubDefaultMediationSetting.CommandDefaultSetting,  CancellationToken.None);
        
        Assert.NotNull(result);
        Assert.Equal(string.Empty, await result);
    }
}