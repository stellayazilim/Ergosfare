namespace Stella.Ergosfare.Core.Abstractions.DispatchRoots;
/// <summary>
/// <see cref="MessageRoot"/> closed over <typeparamref name="TMessage"/>; instantiated by
/// generated registration code.
/// </summary>
/// <typeparam name="TMessage">The message type this root carries.</typeparam>
public sealed class MessageRoot<TMessage> : MessageRoot where TMessage : IMessage
{
    /// <summary>
    /// Calls <paramref name="visitor"/> with <typeparamref name="TMessage"/> as its generic
    /// argument.
    /// </summary>
    /// <typeparam name="TReturn">What the visitor produces.</typeparam>
    /// <typeparam name="TState">The state the visitor needs.</typeparam>
    /// <param name="visitor">The visitor to call.</param>
    /// <param name="state">Passed to the visitor unchanged.</param>
    /// <returns>Whatever the visitor produced.</returns>
    public override TReturn Accept<TReturn, TState>(IMessageRootVisitor<TReturn, TState> visitor, TState state)
        => visitor.Visit<TMessage>(state);
}
