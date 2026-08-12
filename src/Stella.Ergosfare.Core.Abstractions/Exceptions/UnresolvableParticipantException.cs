
namespace Stella.Ergosfare.Core.Abstractions.Exceptions;

/// <summary>
/// Exception thrown when a message's selected frozen composition names a participant that
/// the dispatching container cannot resolve.
/// </summary>
/// <remarks>
/// Raised while the pipeline is being built rather than part-way through a dispatch, so no
/// participant runs before the failure. Nothing is cached for the failed build. Ensure the
/// module that selected the participant also registers it with dependency injection before
/// building the container.
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
