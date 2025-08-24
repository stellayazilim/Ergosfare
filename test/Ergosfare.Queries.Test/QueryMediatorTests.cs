using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Ergosfare.Queries.Abstractions;
using Ergosfare.Queries.Extensions.MicrosoftDependencyInjection;
using Ergosfare.Queries.Test.__stubs__;
using Microsoft.Extensions.DependencyInjection;

namespace Ergosfare.Queries.Test;

public class QueryMediatorTests
{
    [Fact]
    public async Task ShouldResolveTQueryTResult()
    {
        var serviceCollection = new ServiceCollection()
            .AddErgosfare(x => x.AddQueryModule(q => q.Register<StubNonGenericStringResultQueryHandler>()
            )).BuildServiceProvider();
        
        
        var messageMediator = serviceCollection.GetService<IMessageMediator>();
        var mediator = new QueryMediator(messageMediator!);

        var result = mediator.QueryAsync(new StubNonGenericStringResultQuery(), null);
        
        Assert.NotNull(result);
        Assert.Equal(string.Empty, await result);
        
    }
    
    
    [Fact]
    public async Task ShouldResolveTQueryTStreamResult()
    {
        // arrange
        var serviceCollection = new ServiceCollection()
            .AddErgosfare(x => x.AddQueryModule(q => q.Register<StubNonGenericStreamStringResultQueryHandler>()
            )).BuildServiceProvider();
        
        
        var messageMediator = serviceCollection.GetService<IMessageMediator>();
        var mediator = new QueryMediator(messageMediator!);
        var expected = new []  {"Foo", "Bar", "Baz"};
        var result = new List<string>();
        // act

        await foreach (var item in mediator.StreamAsync(new StubNonGenericStreamStringResultQuery(), null))
        {
            result.Add(item);
        }
        
        Assert.NotNull(result);
        Assert.Equal(expected, result);
        
    }
}