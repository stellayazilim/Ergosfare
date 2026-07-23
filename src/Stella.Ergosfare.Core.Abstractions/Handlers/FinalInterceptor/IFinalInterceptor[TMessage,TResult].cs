using System;

namespace Stella.Ergosfare.Core.Abstractions.Handlers;


/// <summary>
/// Synchronous final-interceptor contract for messages of type <typeparamref name="TMessage"/>
/// with results of type <typeparamref name="TResult"/>. Always executed at the end of the
/// pipeline, regardless of success or failure — for cleanup, auditing, or logging.
/// </summary>
/// <typeparam name="TMessage">The type of message this interceptor handles.</typeparam>
/// <typeparam name="TResult">The type of result produced by the handler.</typeparam>
/// <remarks>
/// Final interceptors observe the pipeline outcome but cannot alter it — hence
/// <see cref="Handle"/> returns nothing. Asynchronous final interceptors implement
/// <see cref="IAsyncFinalInterceptor{TMessage}"/> or
/// <see cref="IAsyncFinalInterceptor{TMessage, TResult}"/> instead; the pipeline dispatches
/// each through its own typed member with no object-typed bridge between them.
/// </remarks>
public interface IFinalInterceptor<in TMessage, in TResult> : IFinalInterceptor
{
    /// <summary>
    /// Handles the end of the pipeline for the given message.
    /// </summary>
    /// <param name="message">The message that was processed.</param>
    /// <param name="result">The final result, if any.</param>
    /// <param name="exception">The exception that terminated the pipeline, if any.</param>
    /// <param name="executionContext">The current execution context.</param>
    void Handle(TMessage message, TResult? result, Exception? exception, IExecutionContext executionContext);
}
