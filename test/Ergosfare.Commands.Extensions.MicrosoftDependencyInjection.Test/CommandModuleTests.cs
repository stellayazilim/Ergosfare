using System.Reflection;
using Ergosfare.Commands.Abstractions;
using Ergosfare.Context;
using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Handlers;
using Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Ergosfare.Commands.Extensions.MicrosoftDependencyInjection.Test;

public class CommandModuleTests
{
    
    private class NonCommandHandler: IHandler<IMessage, Task>
    {
        public Task Handle(IMessage message, IExecutionContext context)
        {
            return Task.CompletedTask;
        }
    }
    
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ShouldRegisterCommandModule()
    {
        var serviceCollection = new ServiceCollection()

            .AddErgosfare(x => x.AddCommandModule(c =>
                c.Register<TestCommandHandler>()
                    .RegisterFromAssembly(Assembly.GetExecutingAssembly()
                    )
            )).BuildServiceProvider();


        var mediator = serviceCollection.GetRequiredService<ICommandMediator>();

        var result = mediator.SendAsync(new TestCommand());
        var stringResult = mediator.SendAsync<string>(new TestCommandStringResult());
        
        
        Assert.NotNull(result);
        Assert.NotNull(stringResult);
        Assert.Equal(string.Empty, await stringResult);
        
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ShouldNotRegisterNonCommandsToCommandModule()
    {
        var serviceCollection = new ServiceCollection();

        Assert.Throws<NotSupportedException>(() =>
        {

            serviceCollection.AddErgosfare(x => 
                x.AddCommandModule(c =>
                c.Register(typeof(NonCommandHandler)))).BuildServiceProvider();
        });


    }
}