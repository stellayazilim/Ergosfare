namespace Stella.Ergosfare.Core.Abstractions.DispatchRoots;
/// <summary>
/// Receives a <see cref="MessageResultRoot"/>'s message and result types as generic
/// arguments.
/// </summary>
/// <typeparam name="TReturn">What the visitor produces.</typeparam>
/// <typeparam name="TState">The state the visitor needs.</typeparam>
public interface IMessageResultRootVisitor<out TReturn, in TState>
{
    /// <summary>
    /// Builds the result inside a generic context carrying the root's message and result
    /// types.
    /// </summary>
    /// <typeparam name="TMessage">The root's message type.</typeparam>
    /// <typeparam name="TResult">The root's result type.</typeparam>
    /// <param name="state">The state passed to <see cref="MessageResultRoot.Accept{TReturn, TState}"/>.</param>
    /// <returns>The visitor's result.</returns>
    TReturn Visit<TMessage, TResult>(TState state) where TMessage : IMessage;
}
