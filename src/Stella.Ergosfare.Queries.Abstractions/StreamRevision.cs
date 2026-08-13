namespace Stella.Ergosfare.Queries.Abstractions;

/// <summary>
/// The notice carried by the streaming entry points while the shape of stream messaging is
/// being revised.
/// </summary>
/// <remarks>
/// <para>
/// Marked rather than left silent because the revision will not be source-compatible, and a
/// consumer building on the current shape deserves to know that before the release that
/// changes it — not after.
/// </para>
/// <para>
/// It is a warning, not an error, and streaming keeps working exactly as it does today. The
/// experimental marker the result-adapter and plugin surfaces carry would have been the
/// wrong instrument: it fails the build by default, and this surface has already shipped.
/// </para>
/// <para>
/// The revision's substance: the enumerable shape allocates on every enumeration, and the
/// stream pipeline's own semantics — stream commands, streamed results, what a stage means
/// mid-sequence — are not settled the way the single-result ones are.
/// </para>
/// </remarks>
public static class StreamRevision
{
    public const string Notice =
        "Stream messaging is being revised and its shape will not survive the revision source-compatible. " +
        "It keeps working as-is meanwhile; suppress this warning to opt in until the revision lands.";
}
