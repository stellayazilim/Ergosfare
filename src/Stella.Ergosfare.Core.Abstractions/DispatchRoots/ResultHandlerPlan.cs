namespace Stella.Ergosfare.Core.Abstractions.DispatchRoots;
/// <summary>
/// A compiled plan for a result-producing message whose whole pipeline is one asynchronous
/// handler; the result-producing counterpart of <see cref="VoidHandlerPlan"/>.
/// </summary>
public abstract class ResultHandlerPlan
{
    /// <summary>
    /// Calls <paramref name="visitor"/> with this plan's message, result and handler types
    /// as its generic arguments.
    /// </summary>
    /// <typeparam name="TReturn">What the visitor produces.</typeparam>
    /// <typeparam name="TState">The state the visitor needs.</typeparam>
    /// <param name="visitor">The visitor to call.</param>
    /// <param name="state">Passed to the visitor unchanged.</param>
    /// <returns>Whatever the visitor produced.</returns>
    public abstract TReturn Accept<TReturn, TState>(IResultHandlerPlanVisitor<TReturn, TState> visitor, TState state);

    /// <inheritdoc cref="VoidHandlerPlan.DirectHandlerFactory"/>
    internal virtual object? DirectHandlerFactory => null;
}
