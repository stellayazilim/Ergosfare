using Ergosfare.Context;
using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Test.Fixtures.Stubs.Generic;

public class VoidStubGenericHandler<TMessage>: IHandler<StubGenericMessage<TMessage>, Task>
{
    public Task Handle(StubGenericMessage<TMessage> message, IExecutionContext context)
    {
        return Task.CompletedTask;
    }

}

public class VoidStubGenericPreInterceptor<TMessage>: IPreInterceptor<StubGenericMessage<TMessage>>
{
    public object Handle(StubGenericMessage<TMessage> message, IExecutionContext context)
    {
        return message;
    }
}

public class VoidStubGenericPostInterceptor<TMessage>: IPostInterceptor<StubGenericMessage<TMessage>, Task>
{
    public object Handle(StubGenericMessage<TMessage> message, Task? messageResult, IExecutionContext context)
    {
        return Task.CompletedTask;
    }
}


public class VoidStubGenericExceptionInterceptor<TMessage>: IExceptionInterceptor<StubGenericMessage<TMessage>, Task>
{

    public object Handle(StubGenericMessage<TMessage> message, Task? messageResult, Exception exception, IExecutionContext context)
    {
        return Task.CompletedTask;
    }
}

public class VoidStubGenericFinalInterceptor<TMessage>: IFinalInterceptor<StubGenericMessage<TMessage>, Task>
{
    public object Handle(StubGenericMessage<TMessage> message, Task? result, Exception? exception, IExecutionContext executionContext)
    {
        return Task.CompletedTask;
    }
}


public class VoidMultiStubGenericFinalInterceptor<TMessage>: IFinalInterceptor<StubGenericMessage<TMessage>, Task>
{

    public object Handle(StubGenericMessage<TMessage> message, Task? result, Exception? exception, IExecutionContext executionContext)
    {
        return Task.CompletedTask;
    }
}