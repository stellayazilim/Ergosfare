namespace Stella.Ergosfare.Core.Abstractions;

/// <summary>
/// A compile-time pipeline plan closed over a void message and its sole async handler;
/// see <see cref="GeneratedDispatchRoots.AddVoidPlan{TMessage, THandler}"/> and
/// <see cref="MessageRoot"/> for the visitor re-entry pattern.
/// </summary>
public abstract class VoidPlanRoot
{
    /// <summary>Invokes the visitor with this plan's message and handler types as the generic arguments.</summary>
    public abstract TReturn Accept<TReturn, TState>(IVoidPlanRootVisitor<TReturn, TState> visitor, TState state);

    /// <summary>
    /// The compile-time handler construction path — a <c>Func&lt;THandler&gt;</c> or a
    /// provider-taking <c>Func&lt;IServiceProvider, THandler&gt;</c>, carried erased and
    /// cast back inside the executor's closed generic context — or <c>null</c> when the
    /// generator emitted no factory for the handler.
    /// </summary>
    internal virtual object? DirectHandlerFactory => null;
}
