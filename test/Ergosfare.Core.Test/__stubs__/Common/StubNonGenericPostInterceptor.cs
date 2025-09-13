using Ergosfare.Context;
using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Core.Test.__stubs__;

internal class StubNonGenericPostInterceptor: IAsyncPostInterceptor<StubNonGenericMessage>
{
 
    public Task HandleAsync(StubNonGenericMessage message, object? _, IExecutionContext context)
    {
        return Task.CompletedTask;
    }
}


internal class StubNonGenericDerivedPostInterceptor: IAsyncPostInterceptor<StubNonGenericDerivedMessage>
{
    public Task HandleAsync(StubNonGenericDerivedMessage message, object? messageResult, IExecutionContext context)
    {
        return Task.CompletedTask;
    }

   
}


internal class StubNonGenericStreamPostInterceptorsAbortExecution: IAsyncPostInterceptor<StubNonGenericMessage,IAsyncEnumerable<string>>
{

    public Task<object> HandleAsync(StubNonGenericMessage message, IAsyncEnumerable<string>? messageResult, IExecutionContext context)
    {
        context.Abort();
        return Task.FromResult<object>(messageResult ?? throw new ArgumentNullException(nameof(messageResult)));
    }
}


internal class StubNonGenericStreamPostInterceptorThrowsException: IAsyncPostInterceptor<StubNonGenericMessage,IAsyncEnumerable<string>>
{


    public Task<object> HandleAsync(StubNonGenericMessage message, IAsyncEnumerable<string>? messageResult, IExecutionContext context)
    {
        throw new Exception("post exception");
    }
}

internal class StubNonGenericPostInterceptor2 : StubNonGenericPostInterceptor;
internal class StubNonGenericDerivedPostInterceptor2: StubNonGenericDerivedPostInterceptor;