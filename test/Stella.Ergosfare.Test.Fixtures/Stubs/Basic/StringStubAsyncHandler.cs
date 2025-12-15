using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;

namespace Stella.Ergosfare.Test.Fixtures.Stubs.Basic;

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

/// <summary>
/// A stub implementation of <see cref="IAsyncFinalInterceptor{TMessage, TResult}"/> 
/// for <see cref="StubMessage"/> that produces a string result.
/// </summary>
public class StubStringAsyncFinalInterceptor : IAsyncFinalInterceptor<StubMessage, string>
{
    /// <summary>
    /// A constant string representing a sample result.
    /// </summary>
    public const string Result = "Hello world";
    
    
    /// <summary>
    /// Handles the final interception of a <see cref="StubMessage"/> with a string result.
    /// </summary>
    /// <param name="message">The message being processed.</param>
    /// <param name="result">The result of the message handling, if any.</param>
    /// <param name="exception">Any exception thrown during handling, if any.</param>
    /// <param name="context">The current execution context.</param>
    /// <returns>A completed task.</returns>
    public Task HandleAsync(StubMessage message, string? result, Exception? exception, IExecutionContext context)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// A stub implementation of <see cref="IAsyncExceptionInterceptor{TMessage, TResult}"/> 
/// for <see cref="StubMessage"/> that modifies the result when an exception occurs.
/// </summary>
public class StubStringAsyncExceptionInterceptorModifiesResult : IAsyncExceptionInterceptor<StubMessage, string>
{
    /// <summary>
    /// Handles an exception that occurred during processing of a <see cref="StubMessage"/> 
    /// and returns a modified result.
    /// </summary>
    /// <param name="message">The message being processed.</param>
    /// <param name="result">The original result of the message handling, if any.</param>
    /// <param name="exception">The exception that occurred during handling.</param>
    /// <param name="context">The current execution context.</param>
    /// <returns>A task that completes with the modified result.</returns>
    public Task<object> HandleAsync(StubMessage message, string? result, Exception exception, IExecutionContext context)
    {
        return Task.FromResult<object>("modified result");
    }
}