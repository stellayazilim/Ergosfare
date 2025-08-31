using Ergosfare.Context;
using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Core.Test.__stubs__;

internal class StubNonGenericPostInterceptor: IAsyncPostInterceptor<StubNonGenericMessage,Task>
{
    public async Task HandleAsync(StubNonGenericMessage message, Task? messageResult, IExecutionContext context)
    {
        await Task.Yield();
               
    }
}


internal class StubNonGenericDerivedPostInterceptor: IAsyncPostInterceptor<StubNonGenericDerivedMessage,Task>
{
    public async Task HandleAsync(StubNonGenericDerivedMessage message, Task? messageResult, IExecutionContext context)
    {
        await Task.Yield();
    }
}


internal class StubNonGenericStreamPostInterceptorsAbortExecution: IAsyncPostInterceptor<StubNonGenericMessage,IAsyncEnumerable<string>>
{

    public Task HandleAsync(StubNonGenericMessage message, IAsyncEnumerable<string>? messageResult, IExecutionContext context)
    {
        context.Abort();
        return Task.CompletedTask;
    }
}


internal class StubNonGenericStreamPostInterceptorThrowsException: IAsyncPostInterceptor<StubNonGenericMessage,IAsyncEnumerable<string>>
{


    public Task HandleAsync(StubNonGenericMessage message, IAsyncEnumerable<string>? messageResult, IExecutionContext context)
    {
        throw new Exception("post exception");
    }
}

internal class StubNonGenericPostInterceptor2 : StubNonGenericPostInterceptor;
internal class StubNonGenericDerivedPostInterceptor2: StubNonGenericDerivedPostInterceptor;