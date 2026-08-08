namespace Stella.Ergosfare.Core.Abstractions;

/// <summary>
/// A compile-time pipeline plan closed over a result-producing message, its result type
/// and its sole async handler; the result-producing counterpart of
/// <see cref="VoidPlanRoot"/>.
/// </summary>
public abstract class ResultPlanRoot
{
    /// <summary>Invokes the visitor with this plan's message, result and handler types as the generic arguments.</summary>
    public abstract TReturn Accept<TReturn, TState>(IResultPlanRootVisitor<TReturn, TState> visitor, TState state);

    /// <inheritdoc cref="VoidPlanRoot.DirectHandlerFactory"/>
    internal virtual object? DirectHandlerFactory => null;
}
