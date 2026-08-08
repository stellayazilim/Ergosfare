namespace Stella.Ergosfare.Core.Abstractions.DispatchRoots;
/// <summary>
/// A dispatch root closed over a concrete message type. A consumer implements
/// <see cref="IMessageRootVisitor{TReturn, TState}"/> to re-enter a generic context with
/// the root's type argument and construct its closed dispatch component there — no
/// reflection involved.
/// </summary>
public abstract class MessageRoot
{
    /// <summary>Invokes the visitor with this root's message type as the generic argument.</summary>
    public abstract TReturn Accept<TReturn, TState>(IMessageRootVisitor<TReturn, TState> visitor, TState state);
}
