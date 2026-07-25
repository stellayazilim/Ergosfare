using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Queries.Abstractions;
using Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Queries.Test.__stubs__;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Queries.Test;


/// <summary>
/// Contains unit tests for <see cref="IQueryMediator"/> extension methods,
/// verifying that queries and streaming queries work correctly,
/// both with and without groups.
/// </summary>
public class QueryMediatorExtensionsTests
{
    /// <summary>
    /// Tests that a standard query returns the expected result
    /// and that the handler is invoked.
    /// </summary>
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
    
    /// <summary>
    /// Tests that a standard query with a specific group returns the expected result
    /// and that the handler is invoked.
    /// </summary>
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
    
    /// <summary>
    /// Tests that a streaming query returns the expected sequence of results
    /// and that the handler is invoked.
    /// </summary>
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

    /// <summary>
    /// Tests that a streaming query with a specific group returns the expected sequence of results
    /// and that the handler is invoked.
    /// </summary>
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