using System;
using System.Collections.Generic;
using System.Linq;

namespace Ergosfare.Core.Abstractions.SignalHub.Signals;

/// <summary>
///     Represents a signal that is published at the beginning of handling a message,
///     typically used for event handlers.
/// </summary>
public sealed class BeginHandlingSignal: PipelineSignal
{
    /// <summary>
    ///     Gets or sets the total number of handlers that will process the message.
    ///     Useful primarily for event handling scenarios.
    /// </summary>
    public required ushort HandlerCount { get; init; }


    /// <summary>
    ///     Creates a new instance of <see cref="BeginHandlingSignal"/>.
    /// </summary>
    /// <param name="message">The message being handled.</param>
    /// <param name="result">The current result of message processing, if any.</param>
    /// <param name="handlerCount">The number of handlers that will process the message.</param>
    /// <returns>A new instance of <see cref="BeginHandlingSignal"/>.</returns>
    public static BeginHandlingSignal Create(object message, object? result, ushort handlerCount = 0) => new()
    {
        Message = message ?? throw new ArgumentNullException(nameof(message)),
        Result = result,
        HandlerCount = handlerCount,
    };

    
    /// <summary>
    ///     Publishes a new <see cref="BeginHandlingSignal"/> to the signal hub.
    /// </summary>
    /// <param name="message">The message being handled.</param>
    /// <param name="result">The current result of message processing, if any.</param>
    /// <param name="handlerCount">The number of handlers that will process the message.</param>
    public static void Invoke(object message, object? result, ushort handlerCount = 0) => 
        SignalHubAccessor.Instance.Publish(Create( message, result, handlerCount));

    
    /// <summary>
    ///     Returns the components used to determine equality for this signal.
    /// </summary>
    /// <returns>An enumerable of objects that participate in equality comparison.</returns>
    public override IEnumerable<object> GetEqualityComponents()
        => base.GetEqualityComponents().Concat([HandlerCount]);
}