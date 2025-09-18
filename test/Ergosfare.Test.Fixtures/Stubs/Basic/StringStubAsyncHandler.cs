using Ergosfare.Context;
using Ergosfare.Core.Abstractions.Handlers;

namespace Ergosfare.Test.Fixtures.Stubs.Basic;

/// <summary>
/// A stub async handler for <see cref="StubMessage"/> that returns a fixed string result.
/// Useful for testing result propagation and adapters.
/// </summary>
public class StubStringAsyncHandler : IAsyncHandler<StubMessage, string>
{
    /// <summary>
    /// The predefined result returned by this handler.
    /// </summary>
    public const string Result = "Hello world";


    /// <summary>
    /// Handles a <see cref="StubMessage"/> asynchronously and returns a string result.
    /// </summary>
    /// <param name="message">The message to handle.</param>
    /// <param name="context">The execution context.</param>
    /// <returns>A <see cref="Task{TResult}"/> with the result string.</returns>
    public async Task<string> HandleAsync(StubMessage message, IExecutionContext context)
    {
        await Task.CompletedTask;
        return Result;
    }
}


public class StubStringAsyncFinalInterceptor : IAsyncFinalInterceptor<StubMessage, string>
{
    public const string Result = "Hello world";
    public Task HandleAsync(StubMessage message, string? result, Exception? exception, IExecutionContext context)
    {
        return Task.CompletedTask;
    }
}


public class StubStringAsyncExceptionInterceptorModifiesResult : IAsyncExceptionInterceptor<StubMessage, string>
{
    public Task<object> HandleAsync(StubMessage message, string? result, Exception exception, IExecutionContext context)
    {
        return Task.FromResult<object>("modified result");
    }
}