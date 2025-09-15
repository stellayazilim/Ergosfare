using Ergosfare.Context;
using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Core.Test.__stubs__;

internal class StubNonGenericPostInterceptor: IAsyncPostInterceptor<StubNonGenericMessage, object>
{
 
    public Task<object> HandleAsync(StubNonGenericMessage message, object? _, IExecutionContext context)
    {
        return Task.FromResult<object>(new object());
    }
}


internal class StubNonGenericDerivedPostInterceptor: IAsyncPostInterceptor<StubNonGenericDerivedMessage>
{
    public Task<object> HandleAsync(StubNonGenericDerivedMessage message, object? messageResult, IExecutionContext context)
    {
        return Task.FromResult<object>(new object());
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