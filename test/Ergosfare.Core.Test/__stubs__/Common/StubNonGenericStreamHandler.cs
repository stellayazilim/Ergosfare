using System.Runtime.CompilerServices;
using Ergosfare.Context;
using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Core.Test.__stubs__;

internal class StubNonGenericStreamHandler: IStreamHandler<StubNonGenericMessage, string>
{
    public async IAsyncEnumerable<string> StreamAsync(StubNonGenericMessage message, IExecutionContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {


        var results = new string[] {"foo", "bar", "baz"};


        foreach (var result in results)
        {
            await Task.Delay(10, cancellationToken);
            yield return result;
        }   

    }
}


internal class StubNonGenericStreamHandler2: StubNonGenericStreamHandler;


internal class StubNonGenericStreamHandlerAbortsExecution:  IStreamHandler<StubNonGenericMessage, string>
{
    public async IAsyncEnumerable<string> StreamAsync(StubNonGenericMessage message, IExecutionContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var results = new string[] {"foo", "bar", "baz"};


        foreach (var result in results)
        {
            if (result == "bar")
            {
                await Task.Delay(10, cancellationToken);
                context.Abort();
            }
            await Task.Delay(10, cancellationToken);
            yield return result;
        }   
    }
}


internal class StubNonGenericStreamHandlerThrowsException:IStreamHandler<StubNonGenericMessage, string>
{
    public async IAsyncEnumerable<string> StreamAsync(StubNonGenericMessage message, IExecutionContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var results = new string[] {"foo", "bar", "baz"};


        foreach (var result in results)
        {
            if (result == "bar")
            {
               throw new Exception("bar exception");
            }
            await Task.Delay(10, cancellationToken);
            yield return result;
        }   
    }
}