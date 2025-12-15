using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Internal.Contexts;

// ReSharper disable ClassNeverInstantiated.Global

namespace Stella.Ergosfare.Test.Fixtures.Stubs.Basic;

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


public class StubVoidHandlerThrows : IHandler<StubMessage, object>
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
        throw new Exception("Stub exception");
    }
}


public class StubVoidHandlerThrowsWithSnapshot(
    ISnapshotService snapshotService) : 
    IAsyncHandler<StubMessage>
{
    public static bool FirstCheckpointRun;
    public static bool SecondCheckpointRun;

    /// <summary>
    /// Handles a <see cref="StubMessage"/> synchronously.
    /// This implementation does nothing and returns <c>null</c>.
    /// </summary>
    /// <param name="message">The message to handle.</param>
    /// <param name="context">The execution context.</param>
    /// <returns>Always returns <c>null</c>, since no result is produced.</returns>
    // root checkpoint here, entire method body
    public async Task HandleAsync(StubMessage message, IExecutionContext context)
    {
        var result = await snapshotService.Snapshot("test", new Snapshot<bool>(async () => {
            FirstCheckpointRun = true;
            await Task.CompletedTask;
            //throw new Exception("Stub exception");

            return true;
        }));
        
       
    }
}


public class StubVoidIndirectHandler: IHandler<StubIndirectMessage, Task>
{
    public Task Handle(StubIndirectMessage message, IExecutionContext context)
    {
        return Task.CompletedTask;
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
    
    public const string Result = "result";
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
/// Returns <c>messageResult</c> and is used for testing exception interception in handlers.
/// </summary>
public class StubExceptionInterceptor: IExceptionInterceptor<StubMessage, object>
{
    public static object Result;
    public static bool IsCalled;
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
        IsCalled = true;
        Result = messageResult;
        return Task.FromResult(messageResult);
    }
}

/// <summary>
/// A stub implementation of <see cref="IFinalInterceptor{TMessage, TResult}"/> for <see cref="StubMessage"/>.
/// Returns <c>null</c> and is used for testing final interception logic after handler and exception processing.
/// </summary>
public class StubFinalInterceptor: IFinalInterceptor<StubMessage, object>
{

    public static bool IsCalled;
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
        IsCalled = true;
        return null!;
    }
}

/// <summary>
/// A stub implementation of <see cref="IFinalInterceptor{TMessage, TResult}"/> for <see cref="StubIndirectMessage"/>.
/// Returns <c>null</c> and is used for testing final interception logic after handler and exception processing.
/// </summary>
public class StubIndirectFinalInterceptor: IFinalInterceptor<StubIndirectMessage, object>
{
    
    public static bool IsCalled;
    
    /// <summary>
    /// Handles final interception of a message, optionally receiving the result or exception.
    /// </summary>
    /// <param name="message">The message being handled.</param>
    /// <param name="result">The result from the handler or previous interceptors.</param>
    /// <param name="exception">The exception, if one occurred during handling.</param>
    /// <param name="executionContext">The execution context.</param>
    /// <returns>Always returns <c>null</c>.</returns>
    public object Handle(StubIndirectMessage message, object? result, Exception? exception, IExecutionContext executionContext)
    {
        IsCalled = true;
        return null!;
    }
}

