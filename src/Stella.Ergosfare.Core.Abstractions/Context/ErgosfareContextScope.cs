
namespace Stella.Ergosfare.Core.Abstractions;

/// <summary>
/// The child context of a nested dispatch, together with the lifetime that ends it. Open
/// one with <see cref="ErgosfareContext.CreateScope"/>, pass <see cref="Context"/> to the
/// inner mediator call, and dispose the scope when that call completes.
/// </summary>
/// <remarks>
/// The child starts with no items, so nested work is isolated by default, and inherits the
/// parent's cancellation token. Disposing recycles the child, which must not be used
/// afterwards; the parent context is untouched throughout, including when the nested
/// pipeline aborts. The scope is a struct, so opening one allocates nothing.
/// </remarks>
public readonly struct ErgosfareContextScope : IDisposable
{
    /// <summary>
    /// The child context to pass to the nested dispatch.
    /// </summary>
    public ErgosfareContext Context { get; }

    /// <summary>
    /// Pairs a rented child context with the dispose that recycles it.
    /// </summary>
    /// <param name="context">The rented child context.</param>
    internal ErgosfareContextScope(ErgosfareContext context)
    {
        Context = context;
    }

    /// <summary>
    /// Ends the scope and recycles the child context, which must not be used afterwards.
    /// </summary>
    /// <remarks>
    /// Disposing a <c>default(ErgosfareContextScope)</c> — one that never came from
    /// <see cref="ErgosfareContext.CreateScope"/> and so holds no context — does nothing,
    /// rather than throwing out of the <c>using</c> block.
    /// </remarks>
    public void Dispose() => Context?.ReturnToPool();
}
