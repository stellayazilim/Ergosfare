namespace Stella.Ergosfare.Core.Abstractions.DispatchRoots;
/// <summary>
/// A dispatch root holding a concrete (message, result) pair as generic arguments; the
/// two-argument counterpart of <see cref="MessageRoot"/>.
/// </summary>
public abstract class MessageResultRoot
{
    /// <summary>
    /// Calls <paramref name="visitor"/> with this root's message and result types as its
    /// generic arguments.
    /// </summary>
    /// <typeparam name="TReturn">What the visitor produces.</typeparam>
    /// <typeparam name="TState">The state the visitor needs.</typeparam>
    /// <param name="visitor">The visitor to call.</param>
    /// <param name="state">Passed to the visitor unchanged.</param>
    /// <returns>Whatever the visitor produced.</returns>
    public abstract TReturn Accept<TReturn, TState>(IMessageResultRootVisitor<TReturn, TState> visitor, TState state);
}
