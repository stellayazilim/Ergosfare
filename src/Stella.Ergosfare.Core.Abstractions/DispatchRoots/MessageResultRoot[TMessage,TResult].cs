namespace Stella.Ergosfare.Core.Abstractions.DispatchRoots;
/// <summary>
/// <see cref="MessageResultRoot"/> closed over a concrete pair; instantiated by generated
/// registration code.
/// </summary>
/// <typeparam name="TMessage">The message type this root carries.</typeparam>
/// <typeparam name="TResult">The result type this root carries.</typeparam>
public sealed class MessageResultRoot<TMessage, TResult> : MessageResultRoot where TMessage : IMessage
{
    /// <summary>
    /// Calls <paramref name="visitor"/> with <typeparamref name="TMessage"/> and
    /// <typeparamref name="TResult"/> as its generic arguments.
    /// </summary>
    /// <typeparam name="TReturn">What the visitor produces.</typeparam>
    /// <typeparam name="TState">The state the visitor needs.</typeparam>
    /// <param name="visitor">The visitor to call.</param>
    /// <param name="state">Passed to the visitor unchanged.</param>
    /// <returns>Whatever the visitor produced.</returns>
    public override TReturn Accept<TReturn, TState>(IMessageResultRootVisitor<TReturn, TState> visitor, TState state)
        => visitor.Visit<TMessage, TResult>(state);
}
