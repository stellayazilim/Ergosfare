using System;
using System.Collections.Generic;
using System.Linq;

namespace Stella.Ergosfare.Core.Abstractions.SignalHub.Signals;

/// <summary>
/// Represents a signal published after a handler has finished processing a message in the pipeline.
/// </summary>
public sealed class FinishHandlerInvocationSignal: PipelineSignal
{
    /// <summary>
    /// Gets or sets the type of the handler that finished execution.
    /// </summary>
    public required Type HandlerType { get; init; }

    
    /// <summary>
    /// Creates a new instance of <see cref="FinishHandlerInvocationSignal"/> with the specified message, result, and handler type.
    /// </summary>
    /// <param name="message">The message being processed in the pipeline.</param>
    /// <param name="result">The current result of message processing, if any.</param>
    /// <param name="handlerType">The type of the handler that finished execution.</param>
    /// <returns>A new <see cref="FinishHandlerInvocationSignal"/> instance.</returns>
    public static FinishHandlerInvocationSignal Create(object message, object?  result, Type? handlerType) => new()
    {
        Message = message ?? throw new ArgumentNullException(nameof(message)),
        Result = result,
        HandlerType = handlerType ?? throw new ArgumentNullException(nameof(handlerType)),
    };
    
    
    /// <summary>
    /// Publishes a new <see cref="FinishHandlerInvocationSignal"/> to the signal hub.
    /// </summary>
    /// <param name="message">The message being processed in the pipeline.</param>
    /// <param name="result">The current result of message processing, if any.</param>
    /// <param name="handlerType">The type of the handler that finished execution.</param>
    public static void Invoke(object message, object?  result, Type? handlerType) => SignalHubAccessor.Instance.Publish(
        new FinishHandlerInvocationSignal()
    {
        Message = message ?? throw new ArgumentNullException(nameof(message)),
        Result = result,
        HandlerType = handlerType ?? throw new ArgumentNullException(nameof(handlerType)),
    });

    
    /// <summary>
    /// Returns the components used to determine equality for this signal.
    /// </summary>
    public override IEnumerable<object> GetEqualityComponents()
        => base.GetEqualityComponents().Concat(
            [HandlerType]);
}