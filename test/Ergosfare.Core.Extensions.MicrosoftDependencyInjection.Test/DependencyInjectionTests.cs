

using System.Reflection;
using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Strategies;
using Microsoft.Extensions.DependencyInjection;

namespace Ergosfare.Core.Extensions.MicrosoftDependencyInjection.Test;

public class DependencyInjectionTests
{
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void ErgosfareShouldRegistered()
    {
        var serviceProvider = new ServiceCollection()
            .AddErgosfare(c =>
            {
                c.AddCoreModule(b =>
                {
                    b.Register(typeof(MessageHandler));
                    b.Register<MessageHandler>();
                    b.RegisterFromAssembly(Assembly.GetExecutingAssembly());
                });
            })
            .BuildServiceProvider();

        var mediator = serviceProvider.GetRequiredService<IMessageMediator>();


        
        var options = new MediateOptions<Message, Task>
        {
            MessageResolveStrategy = serviceProvider.GetRequiredService<ActualTypeOrFirstAssignableTypeMessageResolveStrategy>(),
            MessageMediationStrategy = new SingleAsyncHandlerMediationStrategy<Message>(),
            CancellationToken = default,
            Groups = []
        };
        
        var result = mediator.Mediate(new  Message(), options);
        
        Assert.NotNull(result);
        
    }
}