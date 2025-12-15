using System.Reflection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Strategies;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection.Test;

/// <summary>
/// Contains unit tests to verify that Stella.Ergosfare dependency injection
/// and message mediation are correctly configured.
/// </summary>
public class DependencyInjectionTests
{
    
    /// <summary>
    /// Tests that the Stella.Ergosfare DI container correctly registers core modules,
    /// including message handlers, and that the <see cref="IMessageMediator"/>
    /// can mediate a <see cref="Message"/> without returning null.
    /// </summary>
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
            MessageMediationStrategy = new SingleAsyncHandlerMediationStrategy<Message>(new ResultAdapterService()),
            CancellationToken = default,
            Groups = []
        };
        var result = mediator.Mediate(new  Message(), options);
        Assert.NotNull(result);
    }
}