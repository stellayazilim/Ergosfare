using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Test.Fixtures.Stubs.Generic;

/// <summary>
/// A stub generic handler that does nothing.
/// Implements <see cref="IHandler{TMessage, TResult}"/> for <see cref="StubGenericMessage{TMessage}"/>.
/// </summary>
/// <typeparam name="TMessage">The type parameter for the generic message.</typeparam>
public class VoidStubGenericHandler<TMessage> : IHandler<StubGenericMessage<TMessage>, Task>
{
    /// <summary>
    /// Handles the message by doing nothing.
    /// </summary>
    public Task Handle(StubGenericMessage<TMessage> message, IExecutionContext context)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// A stub pre-interceptor that returns the original message.
/// Implements <see cref="IPreInterceptor{TMessage}"/>.
/// </summary>
/// <typeparam name="TMessage">The type parameter for the generic message.</typeparam>
public class VoidStubGenericPreInterceptor<TMessage> : IPreInterceptor<StubGenericMessage<TMessage>>
{
    public object Handle(StubGenericMessage<TMessage> message, IExecutionContext context)
    {
        return message;
    }
}

/// <summary>
/// A stub post-interceptor that returns a completed Task.
/// Implements <see cref="IPostInterceptor{TMessage, TResult}"/>.
/// </summary>
/// <typeparam name="TMessage">The type parameter for the generic message.</typeparam>
public class VoidStubGenericPostInterceptor<TMessage> : IPostInterceptor<StubGenericMessage<TMessage>, Task>
{
    public object Handle(StubGenericMessage<TMessage> message, Task? messageResult, IExecutionContext context)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// A stub exception interceptor that returns a completed Task.
/// Implements <see cref="IExceptionInterceptor{TMessage, TResult}"/>.
/// </summary>
/// <typeparam name="TMessage">The type parameter for the generic message.</typeparam>
public class VoidStubGenericExceptionInterceptor<TMessage> : IExceptionInterceptor<StubGenericMessage<TMessage>, Task>
{
    public object Handle(StubGenericMessage<TMessage> message, Task? messageResult, Exception exception, IExecutionContext context)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// A stub final interceptor that returns a completed Task.
/// Implements <see cref="IFinalInterceptor{TMessage, TResult}"/>.
/// </summary>
/// <typeparam name="TMessage">The type parameter for the generic message.</typeparam>
public class VoidStubGenericFinalInterceptor<TMessage> : IFinalInterceptor<StubGenericMessage<TMessage>, Task>
{
    public object Handle(StubGenericMessage<TMessage> message, Task? result, Exception? exception, IExecutionContext executionContext)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// A stub final interceptor that can be used for multiple registrations.
/// Implements <see cref="IFinalInterceptor{TMessage, TResult}"/>.
/// </summary>
/// <typeparam name="TMessage">The type parameter for the generic message.</typeparam>
public class VoidMultiStubGenericFinalInterceptor<TMessage> : IFinalInterceptor<StubGenericMessage<TMessage>, Task>
{
    public object Handle(StubGenericMessage<TMessage> message, Task? result, Exception? exception, IExecutionContext executionContext)
    {
        return Task.CompletedTask;
    }
}