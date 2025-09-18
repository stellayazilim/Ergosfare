using Ergosfare.Context;
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


public class StubVoidAsyncPreInterceptor: IAsyncPreInterceptor<StubMessage>
{
    public Task<object> HandleAsync(StubMessage message, IExecutionContext context)
    {
        return Task.FromResult<object>(message);
    }
}

public class StubVoidAsyncPostInterceptor: IAsyncPostInterceptor<StubMessage>
{
    public Task<object> HandleAsync(StubMessage message, object? result, IExecutionContext context)
    {
        return Task.FromResult(result!);
    }
}

public class StubVoidAsyncExceptionInterceptor: IAsyncExceptionInterceptor<StubMessage>
{
    public Task HandleAsync(StubMessage message, object? messageResult, Exception exception, IExecutionContext context)
    {
        return Task.CompletedTask;
    }
}

public class StubVoidAsyncFinalInterceptor: IAsyncFinalInterceptor<StubMessage>
{
    public Task HandleAsync(StubMessage message, object? result, Exception? exception, IExecutionContext context)
    {
        return Task.CompletedTask;
    }
}
public class StubVoidIndirectAsyncFinalInterceptor: IAsyncFinalInterceptor<StubIndirectMessage>
{
    public Task HandleAsync(StubIndirectMessage message, object? result, Exception? exception, IExecutionContext context)
    {
        return Task.CompletedTask;
    }
}