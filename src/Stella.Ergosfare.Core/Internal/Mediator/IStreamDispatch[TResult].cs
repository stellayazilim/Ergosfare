using Stella.Ergosfare.Core.Abstractions;

namespace Stella.Ergosfare.Core.Internal.Mediator;

/// <summary>
/// One container's streaming pipeline for a (query, item) pair, seen through its item type
/// so the table can return it without naming the query type.
/// </summary>
/// <typeparam name="TResult">The type of the streamed items.</typeparam>
internal interface IStreamDispatch<out TResult>
{
    /// <summary>
    /// Streams the results of <paramref name="query"/>.
    /// </summary>
    /// <param name="query">The query to run.</param>
    /// <param name="context">
    /// The caller's execution context, or <c>null</c> to create one for this stream.
    /// </param>
    /// <param name="cancellationToken">Token for the enumeration.</param>
    /// <param name="serviceProvider">The provider participants are resolved from.</param>
    /// <param name="groups">The groups to run, or <c>null</c> for the default.</param>
    /// <returns>The streamed results.</returns>
    IAsyncEnumerable<TResult> Stream(
        object query,
        ErgosfareContext? context,
        CancellationToken cancellationToken,
        IServiceProvider serviceProvider,
        IEnumerable<string>? groups);
}
