using Ergosfare.Core;
using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.EventHub;
using Ergosfare.Core.Abstractions.Strategies;
using Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Ergosfare.Queries.Abstractions;
using Ergosfare.Queries.Extensions.MicrosoftDependencyInjection;
using Ergosfare.Queries.Test.__stubs__;
using Microsoft.Extensions.DependencyInjection;

namespace Ergosfare.Queries.Test;


/// <summary>
/// Contains unit tests for the <see cref="QueryMediator"/> class,
/// verifying that queries and streaming queries are correctly resolved
/// and executed.
/// </summary>
public class QueryMediatorTests
{
    /// <summary>
    /// Verifies that a standard query with a specific result type is resolved correctly
    /// and returns the expected value.
    /// </summary>
    [Fact]
    public async Task ShouldResolveTQueryTResult()
    {
        var services = new ServiceCollection()
            .AddErgosfare(x => x.AddQueryModule(q => q.Register<StubNonGenericStringResultQueryHandler>()
            )).BuildServiceProvider();
        var messageMediator = services.GetService<IMessageMediator>();
        var mediator = new QueryMediator(
            services.GetRequiredService<ActualTypeOrFirstAssignableTypeMessageResolveStrategy>(),
            new ResultAdapterService(),
            messageMediator!);
        var result = mediator.QueryAsync(new StubNonGenericStringResultQuery(), null);
        Assert.NotNull(result);
        Assert.Equal(string.Empty, await result); 
    }
    
    /// <summary>
    /// Verifies that a streaming query with a specific result type is resolved correctly,
    /// returns the expected sequence of values, and invokes the handler.
    /// </summary>
    [Fact]
    public async Task ShouldResolveTQueryTStreamResult()
    {
        // arrange
        var serviceCollection = new ServiceCollection()
            .AddErgosfare(x => x.AddQueryModule(q => q.Register<StubNonGenericStreamStringResultQueryHandler>()
            )).BuildServiceProvider();
        var messageMediator = serviceCollection.GetService<IMessageMediator>();
        var mediator = new QueryMediator(
            serviceCollection.GetRequiredService<ActualTypeOrFirstAssignableTypeMessageResolveStrategy>(),
            new ResultAdapterService(),
            messageMediator!);
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