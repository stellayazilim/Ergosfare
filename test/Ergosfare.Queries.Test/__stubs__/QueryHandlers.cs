using System.Runtime.CompilerServices;
using Ergosfare.Context;
using Ergosfare.Contracts;
using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Queries.Test.__stubs__;

public class StubNonGenericStringResultQueryHandler: IQueryHandler<StubNonGenericStringResultQuery, string>
{
    
    public static bool IsCalled;
    public Task<string> HandleAsync(StubNonGenericStringResultQuery message, IExecutionContext context)
    {
        IsCalled = true;
        return Task.FromResult(string.Empty);
    }
}



public class StubNonGenericStreamStringResultQueryHandler: IStreamQueryHandler<StubNonGenericStreamStringResultQuery, string>
{
    public static bool IsCalled;
    public async IAsyncEnumerable<string> StreamAsync(StubNonGenericStreamStringResultQuery message, IExecutionContext context)
    {
        IsCalled = true;
        
        await Task.Delay(1, context.CancellationToken);
        yield return "Foo";
        await Task.Delay(1, context.CancellationToken);
        yield return "Bar";
        await Task.Delay(1, context.CancellationToken);
        yield return "Baz";
    }
}