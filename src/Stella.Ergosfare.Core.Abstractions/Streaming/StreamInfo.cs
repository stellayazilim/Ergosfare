using System.Diagnostics.CodeAnalysis;
namespace Stella.Ergosfare.Core.Abstractions.Streaming;

/// <summary>
/// What a stream carried, read at the point someone asks.
/// </summary>
/// <param name="Chunks">Chunks taken from the stream so far.</param>
/// <param name="Duration">Time from the first chunk to the end, or to now.</param>
/// <param name="Completion">How the stream ended, or <see cref="StreamCompletion.Open"/>.</param>
/// <remarks>
/// <para>
/// This is what a stage gets instead of the chunks themselves. At a failure the count is the
/// position — "it got as far as chunk 4,312" — which is the half of the diagnosis an
/// exception cannot carry on its own. Handing over the last chunk instead would mean either
/// retaining a buffer past its lifetime or copying every chunk against a failure that usually
/// does not come; the counters cost nothing, and whoever threw is free to put the rest in
/// their own exception.
/// </para>
/// <para>
/// It describes one stream. An operation that takes chunks in and hands chunks out has two of
/// these, which is the honest shape: the two directions start, end and fail independently.
/// </para>
/// </remarks>
[Experimental(ExperimentalIds.StreamingSurface)]
public readonly record struct StreamInfo(
    long Chunks,
    TimeSpan Duration,
    StreamCompletion Completion);
