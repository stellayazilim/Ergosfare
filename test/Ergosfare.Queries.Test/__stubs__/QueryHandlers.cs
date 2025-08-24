using System.Runtime.CompilerServices;
using Ergosfare.Context;
using Ergosfare.Contracts;
using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Queries.Test.__stubs__;

public class StubNonGenericStringResultQueryHandler: IQueryHandler<StubNonGenericStringResultQuery, string>
{
    public Task<string> HandleAsync(StubNonGenericStringResultQuery message, IExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(string.Empty);
    }
}



public class StubNonGenericStreamStringResultQueryHandler: IStreamQueryHandler<StubNonGenericStreamStringResultQuery, string>
{

    public async IAsyncEnumerable<string> StreamAsync(StubNonGenericStreamStringResultQuery message, IExecutionContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Delay(1, cancellationToken);
        yield return "Foo";
        await Task.Delay(1, cancellationToken);
        yield return "Bar";
        await Task.Delay(1, cancellationToken);
        yield return "Baz";
    }
}