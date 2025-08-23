using Ergosfare.Context;
using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Core.Test.__stubs__;

internal class StubPreInterceptorThrowsUnknownException: IPreInterceptor<StubNonGenericMessage>
{

    public object Handle(StubNonGenericMessage message, IExecutionContext context)
    {
        throw new Exception("Unknown exception");
    }
}



internal class StubPreInterceptorAbortExecution: IPreInterceptor<StubNonGenericMessage>
{


    public object Handle(StubNonGenericMessage message, IExecutionContext context)
    {
        context.Abort();
        return Task.CompletedTask;
    }
}