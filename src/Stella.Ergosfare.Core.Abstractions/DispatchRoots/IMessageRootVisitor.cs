namespace Stella.Ergosfare.Core.Abstractions.DispatchRoots;
/// <summary>
/// Receives a <see cref="MessageRoot"/>'s message type as a generic argument.
/// </summary>
/// <typeparam name="TReturn">What the visitor produces.</typeparam>
/// <typeparam name="TState">The state the visitor needs.</typeparam>
public interface IMessageRootVisitor<out TReturn, in TState>
{
    /// <summary>
    /// Builds the result inside a generic context carrying the root's message type.
    /// </summary>
    /// <typeparam name="TMessage">The root's message type.</typeparam>
    /// <param name="state">The state passed to <see cref="MessageRoot.Accept{TReturn, TState}"/>.</param>
    /// <returns>The visitor's result.</returns>
    TReturn Visit<TMessage>(TState state) where TMessage : IMessage;
}
