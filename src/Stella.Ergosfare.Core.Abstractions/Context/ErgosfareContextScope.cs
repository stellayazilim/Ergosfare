
namespace Stella.Ergosfare.Core.Abstractions;

/// <summary>
/// A child execution-context scope for nested dispatches: the handler opens a scope,
/// passes <see cref="Context"/> to the inner mediator call, and disposes the scope when
/// done. The child starts with clean items (isolation by default) and inherits the
/// parent's cancellation token, so nested work stays on the outer cancellation chain.
/// Disposing returns the child to the pool — the context must not be used after the
/// scope is disposed.
/// </summary>
/// <remarks>
/// The scope is a struct: <c>using var scope = ctx.CreateScope();</c> allocates nothing.
/// An <c>Abort()</c> inside the child only aborts the inner pipeline; nothing ambient is
/// overwritten, so there is no restore step — the parent context stays untouched in the
/// caller's parameter.
/// </remarks>
public readonly struct ErgosfareContextScope : IDisposable
{
    /// <summary>The child execution context to pass to nested mediator calls.</summary>
    public ErgosfareContext Context { get; }

    /// <summary>
    /// Wraps a rented child context in a scope. Scopes come from
    /// <see cref="ErgosfareContext.CreateScope"/>, which is the only thing that may pair a
    /// pooled context with the dispose that returns it.
    /// </summary>
    internal ErgosfareContextScope(ErgosfareContext context)
    {
        Context = context;
    }

    /// <summary>
    /// Ends the scope, returning the child context to the pool. The context must not
    /// be used afterwards.
    /// </summary>
    /// <remarks>
    /// Null-conditional for the one shape that has no context to return: a
    /// <c>default(ErgosfareContextScope)</c> that never came from <c>CreateScope()</c>.
    /// Disposing that stays the no-op it has always been rather than throwing out of a
    /// <c>using</c> and masking whatever the block was really doing.
    /// </remarks>
    public void Dispose() => Context?.ReturnToPool();
}
