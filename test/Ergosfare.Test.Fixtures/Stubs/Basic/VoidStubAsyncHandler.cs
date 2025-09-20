using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Test.Fixtures.Stubs.Basic;


/// <summary>
/// A stub async handler for <see cref="StubMessage"/> that performs no operations.
/// Useful for testing handler registration and invocation without side effects.
/// </summary>
public class StubVoidAsyncHandler: IAsyncHandler<StubMessage>
{
    /// <summary>
    /// Handles a <see cref="StubMessage"/> asynchronously.
    /// This implementation completes immediately without performing any action.
    /// </summary>
    /// <param name="message">The message to handle.</param>
    /// <param name="context">The execution context.</param>
    /// <returns>A completed <see cref="Task"/>.</returns>
    public Task HandleAsync(StubMessage message, IExecutionContext context)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// A stub implementation of <see cref="IAsyncPreInterceptor{TMessage}"/> for <see cref="StubMessage"/>.
/// Returns the original message as the result.
/// </summary>
public class StubVoidAsyncPreInterceptor: IAsyncPreInterceptor<StubMessage>
{
    /// <inheritdoc />
    public Task<object> HandleAsync(StubMessage message, IExecutionContext context)
    {
        return Task.FromResult<object>(message);
    }
}

/// <summary>
/// A stub implementation of <see cref="IAsyncPostInterceptor{TMessage}"/> for <see cref="StubMessage"/>.
/// Returns the result of message handling unmodified.
/// </summary>
public class StubVoidAsyncPostInterceptor: IAsyncPostInterceptor<StubMessage>
{
    /// <inheritdoc />
    public Task<object> HandleAsync(StubMessage message, object? result, IExecutionContext context)
    {
        return Task.FromResult(result!);
    }
}

/// <summary>
/// A stub implementation of <see cref="IAsyncExceptionInterceptor{TMessage}"/> for <see cref="StubMessage"/>.
/// Returns the result unmodified when an exception occurs.
/// </summary>
public class StubVoidAsyncExceptionInterceptor: IAsyncExceptionInterceptor<StubMessage>
{
    /// <inheritdoc />
    public Task<object> HandleAsync(StubMessage message, object? messageResult, Exception exception, IExecutionContext context)
    {
        return Task.FromResult<object>(messageResult!);
    }
}

/// <summary>
/// A stub implementation of <see cref="IAsyncFinalInterceptor{TMessage}"/> for <see cref="StubMessage"/>.
/// Does nothing and completes immediately.
/// </summary>
public class StubVoidAsyncFinalInterceptor: IAsyncFinalInterceptor<StubMessage>
{
    /// <inheritdoc />
    public Task HandleAsync(StubMessage message, object? result, Exception? exception, IExecutionContext context)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// A stub implementation of <see cref="IAsyncFinalInterceptor{TMessage}"/> for <see cref="StubIndirectMessage"/>.
/// Does nothing and completes immediately.
/// </summary>
public class StubVoidIndirectAsyncFinalInterceptor: IAsyncFinalInterceptor<StubIndirectMessage>
{
    /// <inheritdoc />
    public Task HandleAsync(StubIndirectMessage message, object? result, Exception? exception, IExecutionContext context)
    {
        return Task.CompletedTask;
    }
}