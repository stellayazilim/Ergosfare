using System.Reflection;
using Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Ergosfare.Queries.Test;

public class QueryModuleTests
{

    private record TestGenericQuery(string Name) :  IQuery<string>;

    public record TestNonGenericQuery : IQuery<string>;

    private sealed class TestQueryHandler: IQueryHandler<TestGenericQuery, string>
    {
        public Task<string> HandleAsync(TestGenericQuery query, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(query.Name);
        }
    }
    
    private sealed class TestNonGenericQueryHandler : IQueryHandler<TestNonGenericQuery, string>
    {
        public Task<string> HandleAsync(TestNonGenericQuery message, CancellationToken cancellationToken = default)
        {
            return Task.FromResult("Hello");
        }
    }
    
    [Fact]
    public async Task SouldRegisterResolveAndMediateQuery()
    {
        
        // arrange
        var serviceProvider = new ServiceCollection()
            .AddErgosfare(x => x.AddQueryModule(c => c.RegisterFromAssembly(Assembly.GetExecutingAssembly()))).BuildServiceProvider();
        var queryMediator = serviceProvider.GetRequiredService<IQueryMediator>();
        
        // act
        var result = await queryMediator.QueryAsync(new TestGenericQuery("Test1"));
        
        // assert
        Assert.Equal("Test1", result);
        Assert.NotNull(result);
        await serviceProvider.DisposeAsync();
    }



    [Fact]
    public async Task ShouldRegisterResolveNonGenericQuery()
    {
        // arrange
        var serviceProvider = new ServiceCollection()
            .AddErgosfare(x => x.AddQueryModule(c => c.RegisterFromAssembly(Assembly.GetExecutingAssembly()))).BuildServiceProvider();
        var queryMediator = serviceProvider.GetRequiredService<IQueryMediator>();
        
        // act
        var result = await queryMediator.QueryAsync(new TestNonGenericQuery());

        // assert
        Assert.Equal("Hello", result);
    }


}