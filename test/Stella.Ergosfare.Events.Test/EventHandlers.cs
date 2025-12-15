using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Events.Abstractions;

// ReSharper disable ClassNeverInstantiated.Global

namespace Stella.Ergosfare.Events.Test;

/// <summary>
/// A stub event handler for <see cref="StubNonGenericEvent"/> that tracks execution.
/// </summary>
public class StubNonGenericEventHandler1: IEventHandler<StubNonGenericEvent>
{
    /// <summary>
    /// Gets a value indicating whether the handler has been executed.
    /// </summary>
    public bool IsRuned { get; private set; }
    
    /// <summary>
    /// Handles the event and sets <see cref="IsRuned"/> to true.
    /// </summary>
    public async Task HandleAsync(StubNonGenericEvent message, IExecutionContext context)
    {
        IsRuned = true;
        await Task.CompletedTask;
    }
}


/// <summary>
/// Another stub event handler for <see cref="StubNonGenericEvent"/> that tracks execution.
/// </summary>
public class StubNonGenericEventHandler2: IEventHandler<StubNonGenericEvent>
{
    /// <summary>
    /// Gets a value indicating whether the handler has been executed.
    /// </summary>
    public bool IsRuned { get; private set; }
    
    /// <summary>
    /// Handles the event and sets <see cref="IsRuned"/> to true.
    /// </summary>
    public async Task HandleAsync(StubNonGenericEvent message, IExecutionContext context)
    {
        IsRuned = true;
        await Task.CompletedTask;
    }
}

/// <summary>
/// A stub event handler for <see cref="StubNonGenericEventThrows"/> that always throws an exception.
/// </summary>
public sealed class StubNonGenericEventHandlerThrows: IEventHandler<StubNonGenericEventThrows>
{
    /// <summary>
    /// Gets a value indicating whether the handler has been executed.
    /// </summary>
    public static bool IsRuned { get; private set; }

    /// <summary>
    /// Handles the event and throws an exception to simulate a failing handler.
    /// </summary>
    public Task HandleAsync(StubNonGenericEventThrows message, IExecutionContext context)
    {
        IsRuned = true;
        throw new Exception("Throw exception");
    }
}

/// <summary>
/// A stub event exception interceptor for <see cref="StubNonGenericEventThrows"/>.
/// Tracks if the exception interceptor has been invoked.
/// </summary>
public sealed class StubNonGenericEventExceptionInterceptor: IEventExceptionInterceptor<StubNonGenericEventThrows>
{
    /// <summary>
    /// Gets a value indicating whether the exception interceptor has been executed.
    /// </summary>
    public static bool IsRuned { get; private set; }

    /// <summary>
    /// Handles an exception thrown by an event handler and sets <see cref="IsRuned"/> to true.
    /// </summary>
    public async Task HandleAsync(StubNonGenericEventThrows @event, Task? result, Exception exception, IExecutionContext context)
    {
        IsRuned = true;
        await Task.CompletedTask;
    }
}