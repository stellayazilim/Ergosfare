using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Ergosfare.Queries.Abstractions;
using Ergosfare.Queries.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Ergosfare.Queries.Test;

public class DummyTest(ITestOutputHelper  testOutputHelper)
{
    record DummyQuery(string Name): IQuery<string>;
    class DummyQueryHandler: IQueryHandler<DummyQuery, string>
    {
        public Task<string> HandleAsync(DummyQuery message, IExecutionContext context)
        {
            
            return Task.FromResult(message.Name);
        }
    }

    class DummyQueryPreHandler: IQueryPreInterceptor<DummyQuery, DummyQuery>
    {
        public Task<DummyQuery?> HandleAsync(DummyQuery query, IExecutionContext executionContext)
        {
            if (query.Name == "hello")
            {
                throw new Exception("bla bla");
            }
            
            return Task.FromResult<DummyQuery?>(new DummyQuery("merhaba"));
        }
    }

    class DummyQueryExceptionInterceptor: IQueryExceptionInterceptor<DummyQuery, string>
    {
        public Task<object> HandleAsync(DummyQuery message, string? result, Exception exception, IExecutionContext context)
        {

            return Task.FromResult<object>(result!);
        }
    }

    [Fact]
    public async Task TestQuery()
    {
        var services = new ServiceCollection()
            .AddErgosfare(cfg => cfg.AddQueryModule( builder => builder.Register<DummyQueryHandler>().Register<DummyQueryPreHandler>()))
            .BuildServiceProvider();
        
        var mediator = services.GetService<IQueryMediator>();

        var result = await mediator!.QueryAsync(new DummyQuery("hello"), CancellationToken.None);
        
        Assert.NotEqual("Hello", result);
    }
}