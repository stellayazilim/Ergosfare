namespace Stella.Ergosfare.Core.Internal;

/// <summary>
/// Runtime behavior switches captured at registration time.
/// </summary>
internal sealed class ErgosfareRuntimeOptions
{
    /// <summary>
    /// When <c>true</c>, every handler graph is resolved once and memoized process-wide
    /// regardless of the handlers' registered DI lifetimes (the pre-v1.2 behavior).
    /// When <c>false</c> (default), registered lifetimes are honored: messages whose
    /// handlers and interceptors are all singleton-registered use the memoized fast path,
    /// everything else is resolved from the calling scope's provider.
    /// </summary>
    public bool MemoizeAllHandlers { get; init; }
}
