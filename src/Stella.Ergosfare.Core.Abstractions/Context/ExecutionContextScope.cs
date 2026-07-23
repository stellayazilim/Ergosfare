using System;

namespace Stella.Ergosfare.Core.Abstractions;

/// <summary>
/// A child execution-context scope for nested dispatches: the handler opens a scope,
/// passes <see cref="Context"/> to the inner mediator call, and disposes the scope when
/// done. The child starts with clean items (isolation by default) and inherits the
/// parent's cancellation token, so nested work stays on the outer cancellation chain.
/// Disposing returns a pooled child to its pool — the context must not be used after the
/// scope is disposed.
/// </summary>
/// <remarks>
/// The scope is a struct: <c>using var scope = ctx.CreateScope();</c> allocates nothing.
/// An <c>Abort()</c> inside the child only aborts the inner pipeline; nothing ambient is
/// overwritten, so there is no restore step — the parent context stays untouched in the
/// caller's parameter.
/// </remarks>
public readonly struct ExecutionContextScope : IDisposable
{
    /// <summary>The child execution context to pass to nested mediator calls.</summary>
    public IExecutionContext Context { get; }

    /// <summary>Wraps a child context in a scope. Called by context implementations.</summary>
    public ExecutionContextScope(IExecutionContext context)
    {
        Context = context;
    }

    /// <summary>
    /// Ends the scope, returning a pooled child context to its pool. The context must not
    /// be used afterwards.
    /// </summary>
    public void Dispose() => (Context as IPoolReturnable)?.ReturnToPool();
}

/// <summary>
/// Implemented by pooled execution contexts; <see cref="ExecutionContextScope.Dispose"/>
/// and the dispatch paths return contexts through it.
/// </summary>
internal interface IPoolReturnable
{
    /// <summary>Resets the instance and returns it to its pool.</summary>
    void ReturnToPool();
}
