namespace Stella.Ergosfare.Core.Abstractions.Streaming;

/// <summary>
/// How a stream ended.
/// </summary>
/// <remarks>
/// A stream ends once. There is no resuming a faulted one: the handler runs a single time
/// for the whole sequence, so when it stops there is nobody left to produce the rest.
/// </remarks>
public enum StreamCompletion
{
    /// <summary>The stream is still open.</summary>
    Open = 0,

    /// <summary>Every chunk was produced and the producer closed the channel.</summary>
    Completed = 1,

    /// <summary>The dispatch was cancelled before the producer finished.</summary>
    Cancelled = 2,

    /// <summary>A failure ended the stream; what was already delivered stays delivered.</summary>
    Faulted = 3,
}
