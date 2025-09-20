using System;
using System.Collections.Generic;
using System.Linq;

namespace Ergosfare.Core.Abstractions.SignalHub.Signals;

/// <summary>
///     Represents a signal that is published when a handler is about to be invoked
///     in the pipeline.
/// </summary>
public sealed class BeginHandlerInvocationSignal: PipelineSignal
{
    /// <summary>
    ///     Gets or sets the type of the handler being invoked.
    /// </summary>
    public required Type HandlerType { get; init; }

    /// <summary>
    ///     Creates a new instance of <see cref="BeginHandlerInvocationSignal"/>.
    /// </summary>
    /// <param name="message">The message being processed.</param>
    /// <param name="result">The current result of message processing, if any.</param>
    /// <param name="handlerType">The type of the handler being invoked.</param>
    /// <returns>A new instance of <see cref="BeginHandlerInvocationSignal"/>.</returns>
    public static BeginHandlerInvocationSignal Create(object message, object?  result, Type handlerType) => new()
    {
        Message = message ?? throw new ArgumentNullException(nameof(message)),
        Result = result,
        HandlerType = handlerType ?? throw new ArgumentNullException(nameof(handlerType)),
    };

    /// <summary>
    ///     Publishes a new <see cref="BeginHandlerInvocationSignal"/> to the signal hub.
    /// </summary>
    /// <param name="message">The message being processed.</param>
    /// <param name="result">The current result of message processing, if any.</param>
    /// <param name="handlerType">The type of the handler being invoked.</param>
    public static void Invoke(object message, object? result, Type handlerType) =>
        SignalHubAccessor.Instance.Publish(new BeginHandlerInvocationSignal()
        {
            Message = message ?? throw new ArgumentNullException(nameof(message)),
            Result = result,
            HandlerType = handlerType ?? throw new ArgumentNullException(nameof(handlerType)),
        });
    
    
    /// <summary>
    ///     Returns the components used to determine equality for this signal.
    /// </summary>
    /// <returns>An enumerable of objects that participate in equality comparison.</returns>
    public override IEnumerable<object> GetEqualityComponents()
        => base.GetEqualityComponents().Concat(
            [HandlerType]);
}