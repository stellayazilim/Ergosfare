using System.Collections.Generic;
using System.Threading;

namespace Ergosfare.Core.Abstractions;

/// <summary>
/// Options for controlling the behavior of message mediation for a specific message type and result type.
/// </summary>
/// <typeparam name="TMessage">The type of the message being mediated. Must be non-nullable.</typeparam>
/// <typeparam name="TResult">The type of the result produced by the message mediation.</typeparam>
public sealed class MediateOptions<TMessage, TResult> 
    where TMessage : notnull
{
    
    
    public byte? Retry {  get; init; }
    
    /// <summary>
    /// Gets or sets the groups to which the message belongs.
    /// </summary>
    public required IEnumerable<string> Groups { get; init; }

    /// <summary>
    /// Gets or sets the strategy used to resolve the message handlers.
    /// </summary>
    public required IMessageResolveStrategy MessageResolveStrategy { get; init; }

    /// <summary>
    /// Gets or sets a collection of arbitrary items that can be shared or passed along during mediation.
    /// </summary>
    public IDictionary<object, object?> Items { get; init; } = new Dictionary<object, object?>();

    /// <summary>
    /// Gets or sets the strategy responsible for executing the message mediation.
    /// </summary>
    public required IMessageMediationStrategy<TMessage, TResult> MessageMediationStrategy { get; init; }

    /// <summary>
    /// Gets or sets the cancellation token used to observe cancellation during message mediation.
    /// </summary>
    public required CancellationToken CancellationToken { get; init; } = CancellationToken.None;

    /// <summary>
    /// Gets or sets a value indicating whether to register plain messages on the spot.
    /// </summary>
    /// <remarks>
    /// Plain messages are messages that do not implement any specific message interfaces.
    /// When this option is enabled, such messages will be automatically registered in the registry
    /// when they are first encountered during mediation.
    /// </remarks>
    public bool RegisterPlainMessagesOnSpot { get; init; } 
}
