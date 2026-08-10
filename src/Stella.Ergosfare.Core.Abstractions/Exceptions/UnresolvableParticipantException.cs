
namespace Stella.Ergosfare.Core.Abstractions.Exceptions;

/// <summary>
/// Exception thrown when a message's pipeline names a participant the dispatching
/// container cannot resolve — typically a type added to the process-wide message registry
/// after that container was built.
/// </summary>
/// <remarks>
/// Raised while the pipeline is being built rather than part-way through a dispatch, so no
/// participant runs before the failure. Nothing is cached for the failed build: registering
/// the participant with a container and dispatching again works, which is the only way back
/// since the registry has no removal.
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
    /// Gets the message type whose pipeline could not be built.
    /// </summary>
    public Type MessageType => messageType;

    /// <summary>
    /// Gets the participant the container cannot resolve.
    /// </summary>
    public Type ParticipantType => participantType;
}
