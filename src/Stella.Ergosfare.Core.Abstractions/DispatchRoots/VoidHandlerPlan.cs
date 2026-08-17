namespace Stella.Ergosfare.Core.Abstractions.DispatchRoots;
/// <summary>
/// A compiled plan for a void message whose whole pipeline is one asynchronous handler,
/// holding both types as generic arguments.
/// </summary>
/// <remarks>
/// Rooted by <see cref="GeneratedDispatchRoots.AddVoidPlan{TMessage, THandler}()"/> and
/// consumed through a visitor, the same way <see cref="MessageRoot"/> is.
/// </remarks>
public abstract class VoidHandlerPlan
{
    /// <summary>
    /// Calls <paramref name="visitor"/> with this plan's message and handler types as its
    /// generic arguments.
    /// </summary>
    /// <typeparam name="TReturn">What the visitor produces.</typeparam>
    /// <typeparam name="TState">The state the visitor needs.</typeparam>
    /// <param name="visitor">The visitor to call.</param>
    /// <param name="state">Passed to the visitor unchanged.</param>
    /// <returns>Whatever the visitor produced.</returns>
    public abstract TReturn Accept<TReturn, TState>(IVoidHandlerPlanVisitor<TReturn, TState> visitor, TState state);

    /// <summary>
    /// The way to construct the handler without the container — a <c>Func&lt;THandler&gt;</c>
    /// or a <c>Func&lt;IServiceProvider, THandler&gt;</c> — or <c>null</c> when the
    /// generator emitted none.
    /// </summary>
    /// <remarks>
    /// Held without its type and cast back inside the executor's closed generic context.
    /// </remarks>
    internal virtual object? DirectHandlerFactory => null;
}
