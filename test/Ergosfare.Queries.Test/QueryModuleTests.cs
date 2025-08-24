using System.Reflection;
using Ergosfare.Context;
using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Handlers;
using Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Ergosfare.Queries.Abstractions;
using Ergosfare.Queries.Extensions.MicrosoftDependencyInjection;
using Ergosfare.Queries.Test.__stubs__;
using Microsoft.Extensions.DependencyInjection;

namespace Ergosfare.Queries.Test;

public class QueryModuleTests
{
    private class NonQueryHandler: IHandler<IMessage, Task>
    {
        public Task Handle(IMessage message, IExecutionContext context)
        {
            return Task.CompletedTask;
        }
    }


    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ShouldRegisterQueryModule()
    {
        var serviceCollection = new ServiceCollection()
            .AddErgosfare(
                x => x.AddQueryModule(
                    c => c.Register<StubNonGenericStringResultQueryHandler>()
                        .RegisterFromAssembly(Assembly.GetExecutingAssembly()))
                ).BuildServiceProvider();

        var mediator = serviceCollection.GetRequiredService<IQueryMediator>();
        var result = mediator.QueryAsync(new StubNonGenericStringResultQuery());
        
        Assert.NotNull(result);
        Assert.Equal(string.Empty, await result);
    }


    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void  ShouldNotRegisterNonQueriesToQueryModule()
    {
        var serviceCollection = new ServiceCollection();
        
        Assert.Throws<NotSupportedException>(() =>
        {

            serviceCollection.AddErgosfare(x => 
                x.AddQueryModule(c =>
                    c.Register(typeof(NonQueryHandler)))).BuildServiceProvider();
        });
    }
}