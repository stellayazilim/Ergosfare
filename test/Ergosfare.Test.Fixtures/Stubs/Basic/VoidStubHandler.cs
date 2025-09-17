using Ergosfare.Context;
using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Test.Fixtures.Stubs.Basic;

/// <summary>
/// A stub synchronous handler for <see cref="StubMessage"/> that performs no operations.
/// Useful for testing handler registration and invocation without side effects.
/// </summary>
public class StubVoidHandler : IHandler<StubMessage, object>
{
    /// <summary>
    /// Handles a <see cref="StubMessage"/> synchronously.
    /// This implementation does nothing and returns <c>null</c>.
    /// </summary>
    /// <param name="message">The message to handle.</param>
    /// <param name="context">The execution context.</param>
    /// <returns>Always returns <c>null</c>, since no result is produced.</returns>
    public object Handle(StubMessage message, IExecutionContext context)
    {
        return null!;
    }
}


/// <summary>
/// A stub implementation of <see cref="IPreInterceptor{TMessage}"/> for <see cref="StubMessage"/>.
/// Returns <c>null</c> and is used for testing interceptor registration and invocation.
/// </summary>
public class StubPreInterceptor: IPreInterceptor<StubMessage>
{
    /// <summary>
    /// Handles the specified <see cref="StubMessage"/>.
    /// </summary>
    /// <param name="message">The message to handle.</param>
    /// <param name="context">The execution context.</param>
    /// <returns>Always returns <c>null</c>.</returns>
    public object Handle(StubMessage message, IExecutionContext context)
    {
        return null!;
    }
}


/// <summary>
/// A stub implementation of <see cref="IPostInterceptor{TMessage, TResult}"/> for <see cref="StubMessage"/>.
/// Returns <c>null</c> and is used for testing post-interceptor pipelines.
/// </summary>
public class StubPostInterceptor: IPostInterceptor<StubMessage, object>
{
    /// <summary>
    /// Handles the post-processing of a message result.
    /// </summary>
    /// <param name="message">The message being handled.</param>
    /// <param name="messageResult">The result from the main handler.</param>
    /// <param name="context">The execution context.</param>
    /// <returns>Always returns <c>null</c>.</returns>
    public object Handle(StubMessage message, object? messageResult, IExecutionContext context)
    {
        return null!;
    }
}


/// <summary>
/// A stub implementation of <see cref="IExceptionInterceptor{TMessage, TResult}"/> for <see cref="StubMessage"/>.
/// Returns <c>null</c> and is used for testing exception interception in handlers.
/// </summary>
public class StubExceptionInterceptor: IExceptionInterceptor<StubMessage, object>
{
    /// <summary>
    /// Handles an exception raised during message handling.
    /// </summary>
    /// <param name="message">The message being handled.</param>
    /// <param name="messageResult">The result from the main handler, if any.</param>
    /// <param name="exception">The exception that was thrown.</param>
    /// <param name="context">The execution context.</param>
    /// <returns>Always returns <c>null</c>.</returns>
    public object Handle(StubMessage message, object? messageResult, Exception exception, IExecutionContext context)
    {
        return null!;
    }
}

/// <summary>
/// A stub implementation of <see cref="IFinalInterceptor{TMessage, TResult}"/> for <see cref="StubMessage"/>.
/// Returns <c>null</c> and is used for testing final interception logic after handler and exception processing.
/// </summary>
public class StubFinalInterceptor: IFinalInterceptor<StubMessage, object>
{
    /// <summary>
    /// Handles final interception of a message, optionally receiving the result or exception.
    /// </summary>
    /// <param name="message">The message being handled.</param>
    /// <param name="result">The result from the handler or previous interceptors.</param>
    /// <param name="exception">The exception, if one occurred during handling.</param>
    /// <param name="executionContext">The execution context.</param>
    /// <returns>Always returns <c>null</c>.</returns>
    public object Handle(StubMessage message, object? result, Exception? exception, IExecutionContext executionContext)
    {
        return null!;
    }
}