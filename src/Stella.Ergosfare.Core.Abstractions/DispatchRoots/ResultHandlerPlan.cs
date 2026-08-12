namespace Stella.Ergosfare.Core.Abstractions.DispatchRoots;
/// <summary>
/// A compile-time pipeline plan closed over a result-producing message, its result type
/// and its sole async handler; the result-producing counterpart of
/// <see cref="VoidHandlerPlan"/>.
/// </summary>
public abstract class ResultHandlerPlan
{
    /// <summary>Invokes the visitor with this plan's message, result and handler types as the generic arguments.</summary>
    public abstract TReturn Accept<TReturn, TState>(IResultHandlerPlanVisitor<TReturn, TState> visitor, TState state);

    /// <inheritdoc cref="VoidHandlerPlan.DirectHandlerFactory"/>
    internal virtual object? DirectHandlerFactory => null;
}
