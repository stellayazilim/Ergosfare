using Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Ergosfare.Queries.Abstractions;
using Ergosfare.Queries.Extensions.MicrosoftDependencyInjection;
using Ergosfare.Queries.Test.__stubs__;
using Microsoft.Extensions.DependencyInjection;

namespace Ergosfare.Queries.Test;

public class QueryMediatorExtensionsTests
{
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task QueryMediatorExtensionsShouldQuery()
    {

        var query = new StubNonGenericStringResultQuery();

        var services = new ServiceCollection()
            .AddErgosfare(options => 
                options.AddQueryModule(queryBuilder =>
                {
                    queryBuilder.Register<StubNonGenericStringResultQueryHandler>();
                }))
            .BuildServiceProvider();
        
        
        
        var mediator = services.GetRequiredService<IQueryMediator>();

        var result = await mediator.QueryAsync(query, CancellationToken.None);
        
        
        Assert.Equal(string.Empty, result);
        Assert.True(StubNonGenericStringResultQueryHandler.IsCalled);
        
    }
    
    
    
    
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task QueryMediatorExtensionsShouldQueryWithGroup()
    {

        var query = new StubNonGenericStringResultQuery();

        var services = new ServiceCollection()
            .AddErgosfare(options => 
                options.AddQueryModule(queryBuilder =>
                {
                    queryBuilder.Register<StubNonGenericStringResultQueryHandler>();
                }))
            .BuildServiceProvider();
        
        
        
        var mediator = services.GetRequiredService<IQueryMediator>();

        var result = await mediator.QueryAsync(query,["default"], CancellationToken.None);
        
        
        Assert.Equal(string.Empty, result);
        Assert.True(StubNonGenericStringResultQueryHandler.IsCalled);
        
    }
    
    
    
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task QueryMediatorExtensionsShouldQueryStream()
    {

        var query = new StubNonGenericStreamStringResultQuery();

        var services = new ServiceCollection()
            .AddErgosfare(options => 
                options.AddQueryModule(queryBuilder =>
                {
                    queryBuilder.Register<StubNonGenericStreamStringResultQueryHandler>();
                }))
            .BuildServiceProvider();


        var results = new List<string>();
        var mediator = services.GetRequiredService<IQueryMediator>();

        await foreach (var result in mediator.StreamAsync(query, CancellationToken.None))
        {
            results.Add(result);
        }
        
        
        Assert.Equal(["Foo", "Bar", "Baz"], results);
        Assert.True(StubNonGenericStreamStringResultQueryHandler.IsCalled);
        
    }

    
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task QueryMediatorExtensionsShouldQueryStreamWithGroup()
    {

        var query = new StubNonGenericStreamStringResultQuery();

        var services = new ServiceCollection()
            .AddErgosfare(options => 
                options.AddQueryModule(queryBuilder =>
                {
                    queryBuilder.Register<StubNonGenericStreamStringResultQueryHandler>();
                }))
            .BuildServiceProvider();


        var results = new List<string>();
        var mediator = services.GetRequiredService<IQueryMediator>();

        await foreach (var result in mediator.StreamAsync(query,["default"], CancellationToken.None))
        {
            results.Add(result);
        }
        
        
        Assert.Equal(["Foo", "Bar", "Baz"], results);
        Assert.True(StubNonGenericStreamStringResultQueryHandler.IsCalled);
        
    }

}