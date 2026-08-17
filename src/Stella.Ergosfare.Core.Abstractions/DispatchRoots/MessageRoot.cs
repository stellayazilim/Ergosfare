namespace Stella.Ergosfare.Core.Abstractions.DispatchRoots;
/// <summary>
/// A dispatch root holding one concrete message type as a generic argument.
/// </summary>
/// <remarks>
/// A caller that needs to build something closed over that type implements
/// <see cref="IMessageRootVisitor{TReturn, TState}"/> and passes it to
/// <see cref="Accept{TReturn, TState}"/>, which re-enters a generic context carrying the
/// type — so nothing has to be constructed reflectively.
/// </remarks>
public abstract class MessageRoot
{
    /// <summary>
    /// Calls <paramref name="visitor"/> with this root's message type as its generic
    /// argument.
    /// </summary>
    /// <typeparam name="TReturn">What the visitor produces.</typeparam>
    /// <typeparam name="TState">The state the visitor needs.</typeparam>
    /// <param name="visitor">The visitor to call.</param>
    /// <param name="state">Passed to the visitor unchanged.</param>
    /// <returns>Whatever the visitor produced.</returns>
    public abstract TReturn Accept<TReturn, TState>(IMessageRootVisitor<TReturn, TState> visitor, TState state);
}
