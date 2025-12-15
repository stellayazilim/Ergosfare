using System.Reflection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Queries.Abstractions;
using Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Queries.Test.__stubs__;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Queries.Test;

/// <summary>
/// Contains unit tests for the <see cref="QueryModule"/> registration and validation,
/// ensuring that query handlers are properly registered and non-query handlers are rejected.
/// </summary>
public class QueryModuleTests
{
    /// <summary>
    /// A test handler that does not implement a query interface.
    /// Used to verify that non-query handlers cannot be registered.
    /// </summary>
    private class NonQueryHandler: IHandler<IMessage, Task>
    {
        public Task Handle(IMessage message, IExecutionContext context)
        {
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Tests that a query module can successfully register query handlers
    /// from both explicit registration and assembly scanning.
    /// </summary>
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


    /// <summary>
    /// Tests that attempting to register a non-query handler throws a <see cref="NotSupportedException"/>.
    /// </summary>
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