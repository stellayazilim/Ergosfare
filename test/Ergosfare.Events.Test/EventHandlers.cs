using Ergosfare.Context;
using Ergosfare.Contracts;
using Ergosfare.Core.Abstractions.Handlers;
using Ergosfare.Events.Abstractions;

namespace Ergosfare.Events.Test;

public class StubNonGenericEventHandler1: IEventHandler<StubNonGenericEvent>
{
    public bool IsRuned { get; private set; }
    public Task HandleAsync(StubNonGenericEvent message, IExecutionContext context)
    {
        IsRuned = true;
        return Task.CompletedTask;
    }
}



public class StubNonGenericEventHandler2: IEventHandler<StubNonGenericEvent>
{
    public bool IsRuned { get; private set; }
    public Task HandleAsync(StubNonGenericEvent message, IExecutionContext context)
    {
        IsRuned = true;
        return Task.CompletedTask;
    }
}



public sealed class StubNonGenericEventHandlerThrows: IEventHandler<StubNonGenericEventThrows>
{
    public static bool IsRuned { get; private set; }

    public Task HandleAsync(StubNonGenericEventThrows message, IExecutionContext context)
    {
        IsRuned = true;
        throw new Exception("Throw exception");
    }
}


public sealed class StubNonGenericEventExceptionInterceptor: IEventExceptionInterceptor<StubNonGenericEventThrows>
{
    public static bool IsRuned { get; private set; }


    public Task<object> HandleAsync(StubNonGenericEventThrows message, object? messageResult, Exception exception, IExecutionContext context)
    {
        IsRuned = true;
        return Task.FromResult<object>(messageResult);
    }
}