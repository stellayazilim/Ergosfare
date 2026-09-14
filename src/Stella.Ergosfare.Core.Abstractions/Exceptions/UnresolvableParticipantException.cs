
namespace Stella.Ergosfare.Core.Abstractions.Exceptions;

/// <summary>
/// Thrown when a message's pipeline names a participant that the dispatching container
/// cannot resolve.
/// </summary>
/// <remarks>
/// The failure happens while the pipeline is being built, so no participant runs before
/// it, and nothing is cached for the failed build. Registering a type in the message
/// registry does not register it with dependency injection — the module that puts a
/// participant in the pipeline must also register it with the container.
/// </remarks>
/// <param name="messageType">The message type whose pipeline could not be built.</param>
/// <param name="participantType">The participant the container cannot resolve.</param>
public class UnresolvableParticipantException(Type messageType, Type participantType)
    : InvalidOperationException(
        $"The pipeline for '{messageType.Name}' includes '{participantType.FullName ?? participantType.Name}', " +
        "which this container cannot resolve. Register it with the container: adding a type to the message " +
        "registry does not register it for dependency injection, and the registry is process-wide while " +
        "containers are not.")
{
    /// <summary>
    /// The message type whose pipeline could not be built.
    /// </summary>
    public Type MessageType => messageType;

    /// <summary>
    /// The participant the container cannot resolve.
    /// </summary>
    public Type ParticipantType => participantType;
}
