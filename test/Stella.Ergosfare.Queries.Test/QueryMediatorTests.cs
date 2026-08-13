// Stream messaging is under revision and its entry points carry the notice; these are
// deliberate call sites of the surface as it stands today.
#pragma warning disable CS0618

using Stella.Ergosfare.Core;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Strategies;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Queries.Test.__stubs__;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Queries.Test;


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
        var mediator = new QueryMediator(
            services.GetRequiredService<MessageDispatchEngine>(), services);
        var result = mediator.QueryAsync(new StubNonGenericStringResultQuery(), new ErgosfareContext(null), null);
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
        var mediator = new QueryMediator(
            serviceCollection.GetRequiredService<MessageDispatchEngine>(), serviceCollection);
        var expected = new []  {"Foo", "Bar", "Baz"};
        var result = new List<string>();
        // act
        await foreach (var item in mediator.StreamAsync(new StubNonGenericStreamStringResultQuery(), (IEnumerable<string>?)null, CancellationToken.None))
        {
            result.Add(item);
        }
        Assert.NotNull(result);
        Assert.Equal(expected, result);
    }
}