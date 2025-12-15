using System;

namespace Stella.Ergosfare.Core.Abstractions.Handlers;


/// <summary>
/// Represents a final interceptor in the message pipeline for a specific message and result type.
/// </summary>
/// <typeparam name="TMessage">The type of message this interceptor handles. Must be compatible with <see cref="object"/>.</typeparam>
/// <typeparam name="TResult">The type of result produced by the message handler. May be nullable.</typeparam>
/// <remarks>
/// Final interceptors always execute at the end of the pipeline, after post- and exception interceptors. 
/// They allow observation of the message and its result, logging, metrics collection, or cleanup operations.
/// 
/// This interface extends the non-generic <see cref="IFinalInterceptor"/> and casts the untyped inputs to
/// the specified generic types for convenience and type safety in implementations.
/// </remarks>
public interface IFinalInterceptor<in TMessage,in TResult> : IFinalInterceptor
{
    /// <inheritdoc cref="IFinalInterceptor.Handle"/>
    object IFinalInterceptor.Handle(object message, object? messageResult, Exception? exception, IExecutionContext context)
    {
        return Handle((TMessage) message, (TResult?) messageResult, exception,  context);
    }

    /// <summary>
    /// Handles the message at the end of the pipeline with strongly typed inputs.
    /// </summary>
    /// <param name="message">The message being processed.</param>
    /// <param name="result">The result produced by the handler, if any. May be null.</param>
    /// <param name="exception">The exception thrown during message handling, if any. May be null.</param>
    /// <param name="executionContext">The current execution context.</param>
    /// <returns>An <see cref="object"/> representing the outcome of the final interceptor. Typically ignored as final interceptors do not modify results.</returns>
    object Handle(TMessage message, TResult? result, Exception? exception, IExecutionContext executionContext);
}