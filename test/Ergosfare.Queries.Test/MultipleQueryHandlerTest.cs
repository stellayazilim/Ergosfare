using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Ergosfare.Queries.Test;

public class MultipleQueryHandlerTest
{
    private record TestNonGenericQuery : IQuery<string>;

    private sealed class Test1Handler: IQueryHandler<TestNonGenericQuery, string>
    {
        public Task<string> HandleAsync(TestNonGenericQuery query, CancellationToken cancellationToken = default)
        {
            return Task.FromResult("Hello");
        }
    }

               
    private sealed class Test2Handler:  IQueryHandler<TestNonGenericQuery, string>
    {
            
        public Task<string> HandleAsync(TestNonGenericQuery message, CancellationToken cancellationToken = default)
        {
            return Task.FromResult("Hello");
        }

    }
    
    
    [Fact]
    public async Task ShouldThrowOnMultipleHandler()
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