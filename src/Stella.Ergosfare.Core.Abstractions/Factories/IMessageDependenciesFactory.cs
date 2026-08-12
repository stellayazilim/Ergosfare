namespace Stella.Ergosfare.Core.Abstractions.Factories;

/// <summary>
/// Factory interface for creating <see cref="IMessageDependencies"/> instances.
/// </summary>
public interface IMessageDependenciesFactory
{
    /// <summary>
    /// Creates a <see cref="IMessageDependencies"/> for the given message type.
    /// </summary>
    /// <param name="messageType">The type of the message.</param>
    /// <param name="groups">The groups to filter handlers by.</param>
    /// <returns>A <see cref="IMessageDependencies"/> instance for the specified message.</returns>
    /// <exception cref="Exceptions.NoHandlerFoundException">
    /// No compiled composition serves the message type, nor any of its ancestors.
    /// </exception>
    public IMessageDependencies Create(Type messageType, IEnumerable<string> groups);

    /// <summary>
    /// The non-throwing counterpart of <see cref="Create"/>: <c>null</c> when no compiled
    /// composition serves the message type or any of its ancestors. Events use it — an
    /// event with no subscribers is a legitimate outcome, not a failure.
    /// </summary>
    /// <param name="messageType">The type of the message.</param>
    /// <param name="groups">The groups to filter handlers by.</param>
    public IMessageDependencies? Find(Type messageType, IEnumerable<string> groups);
}