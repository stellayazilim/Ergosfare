namespace Stella.Ergosfare.Core.Abstractions.Factories;

/// <summary>
/// Builds the <see cref="IMessageDependencies"/> of a message type and group set.
/// </summary>
public interface IMessageDependenciesFactory
{
    /// <summary>
    /// Returns the participants that serve <paramref name="messageType"/> under
    /// <paramref name="groups"/>.
    /// </summary>
    /// <param name="messageType">The message type to build for.</param>
    /// <param name="groups">The groups to filter participants by.</param>
    /// <returns>The message's participants, per stage and in invocation order.</returns>
    /// <exception cref="Exceptions.NoHandlerFoundException">
    /// No compiled composition serves the message type or any of its ancestors.
    /// </exception>
    public IMessageDependencies Create(Type messageType, IEnumerable<string> groups);

    /// <summary>
    /// Returns the participants that serve <paramref name="messageType"/> under
    /// <paramref name="groups"/>, or <c>null</c> when nothing serves it.
    /// </summary>
    /// <param name="messageType">The message type to build for.</param>
    /// <param name="groups">The groups to filter participants by.</param>
    /// <returns>The message's participants, or <c>null</c>.</returns>
    /// <remarks>
    /// The counterpart of <see cref="Create"/> for callers to whom an empty pipeline is a
    /// legitimate outcome rather than a failure — publishing an event nobody subscribes to,
    /// for instance.
    /// </remarks>
    public IMessageDependencies? Find(Type messageType, IEnumerable<string> groups);
}
