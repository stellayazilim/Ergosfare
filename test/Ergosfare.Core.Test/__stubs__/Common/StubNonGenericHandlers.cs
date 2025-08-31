using Ergosfare.Context;
using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Core.Test.__stubs__;



internal class StubNonGenericHandler: IHandler<StubNonGenericMessage,  object>
{
    public object Handle(StubNonGenericMessage message, IExecutionContext context)
    {
        return Task.CompletedTask;
    }
}


internal class StubNonGenericDerivedHandler: IHandler<StubNonGenericDerivedMessage,  object>
{
    public object Handle(StubNonGenericDerivedMessage message, IExecutionContext context)
    {
        return Task.CompletedTask;
    }
}

internal class StubNonGenericStringResultHandler: IHandler<StubNonGenericMessage, string>
{
    public string Handle(StubNonGenericMessage message, IExecutionContext context)
    {
        return string.Empty;
    }
}



internal class StubNonGenericAsyncHandler: IAsyncHandler<StubNonGenericMessage>
{
    public Task HandleAsync(StubNonGenericMessage message, IExecutionContext context)
    {
        return Task.CompletedTask;
    }
}

internal class StubNonGenericStringResultAsyncHandler: IAsyncHandler<StubNonGenericMessage, string>
{
    public Task<string> HandleAsync(StubNonGenericMessage message, IExecutionContext context)
    {
        return Task.FromResult(string.Empty);
    }
}



internal class StubNonGenericStringResultDerivedAsyncHandler: IAsyncHandler<StubNonGenericDerivedMessage>
{
    public Task HandleAsync(StubNonGenericDerivedMessage message, IExecutionContext context)
    {
        return Task.CompletedTask;
    }
}


internal class StubNonGenericHandler2: StubNonGenericHandler;