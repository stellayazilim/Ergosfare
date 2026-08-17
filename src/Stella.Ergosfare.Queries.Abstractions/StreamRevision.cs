namespace Stella.Ergosfare.Queries.Abstractions;

/// <summary>
/// The warning the streaming entry points carry while the shape of stream messaging is
/// being reworked.
/// </summary>
/// <remarks>
/// <para>
/// Streaming keeps working exactly as it does today; this is a warning rather than an
/// error. It exists because the rework will not be source-compatible, and code built on the
/// current shape is better told before the release that changes it than after.
/// </para>
/// <para>
/// The experimental marker used by the result-adapter and plugin surfaces would have been
/// wrong here: it fails the build by default, and this surface has already shipped.
/// </para>
/// <para>
/// What is being reworked: the enumerable shape allocates on every enumeration, and the
/// semantics of a streaming pipeline — stream commands, streamed results, what a stage even
/// means part-way through a sequence — are not settled the way the single-result ones are.
/// </para>
/// </remarks>
public static class StreamRevision
{
    /// <summary>
    /// The warning text, used as the obsoletion message on the streaming entry points.
    /// </summary>
    public const string Notice =
        "Stream messaging is being revised and its shape will not survive the revision source-compatible. " +
        "It keeps working as-is meanwhile; suppress this warning to opt in until the revision lands.";
}
