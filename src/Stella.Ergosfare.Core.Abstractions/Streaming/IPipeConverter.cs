namespace Stella.Ergosfare.Core.Abstractions.Streaming;

/// <summary>Reads a byte source as typed items, preserving decoding state between reads.</summary>
/// <typeparam name="T">The items produced by the converter.</typeparam>
/// <remarks>The converter owns its temporary buffers, but must leave the source open.
/// Published items must remain valid after subsequent reads.</remarks>
[Obsolete("Experimental API: subject to change or removal in any release.", false, DiagnosticId = ExperimentalIds.StreamingSurface)]
public interface IPipeConverter<out T>
{
    /// <summary>Reads until EOF or cancellation; failures propagate to the input consumer.</summary>
    /// <param name="source">The caller-owned byte stream.</param>
    /// <param name="cancellationToken">Cancels reads and converter work.</param>
    /// <returns>The converted items.</returns>
    IAsyncEnumerable<T> ConvertAsync(Stream source, CancellationToken cancellationToken = default);
}

/// <summary>Converts one source item into one destination item.</summary>
/// <typeparam name="TSource">The source item type.</typeparam>
/// <typeparam name="T">The destination item type.</typeparam>
[Obsolete("Experimental API: subject to change or removal in any release.", false, DiagnosticId = ExperimentalIds.StreamingSurface)]
public interface IPipeConverter<in TSource, out T>
{
    /// <summary>Converts a single item. Failures terminate the pipe.</summary>
    /// <param name="chunk">The source item.</param>
    /// <returns>The converted item.</returns>
    T Convert(TSource chunk);
}
