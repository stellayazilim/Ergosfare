namespace Stella.Ergosfare.Core.Abstractions.DispatchRoots;
/// <summary>
/// A dispatch root closed over a concrete (message, result) pair; see
/// <see cref="MessageRoot"/>.
/// </summary>
public abstract class MessageResultRoot
{
    /// <summary>Invokes the visitor with this root's message and result types as the generic arguments.</summary>
    public abstract TReturn Accept<TReturn, TState>(IMessageResultRootVisitor<TReturn, TState> visitor, TState state);
}
