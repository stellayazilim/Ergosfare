using System.Collections.Generic;
using System.Threading;

namespace Ergosfare.Messaging.Abstractions;

public sealed class MediateOptions<TMessage, TResult> 
    where TMessage: IMessage
{
    public required IMessageResolveStrategy MessageResolveStrategy { get; init; }
    
    public IDictionary<object, object?> Items { get; init; } = new Dictionary<object, object?>();
    
    public required IMessageMediationStrategy<TMessage, TResult> MessageMediationStrategy { get; init; }
    
    public required CancellationToken CancellationToken { get; init; } = CancellationToken.None;
    
    
    /// <summary>
    ///     Gets or initializes a value indicating whether to register plain messages on the spot.
    /// </summary>
    /// <remarks>
    ///     Plain messages are messages that do not implement any specific message interfaces.
    ///     When this option is enabled, such messages will be automatically registered in the registry
    ///     when they are first encountered during mediation.
    /// </remarks>
    public bool RegisterPlainMessagesOnSpot { get; init; } = false;

}