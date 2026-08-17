namespace Stella.Ergosfare.Core.Abstractions.Results;

/// <summary>
/// Lets a built-in result carrier name the adapter that reads it, so the pairing can be
/// found from a generic context without reflection.
/// </summary>
/// <remarks>
/// <para>
/// Adapter binding resolves per (message, result) pair, and the carrier's payload type is
/// not among those type parameters — so reaching the payload-closed adapter would otherwise
/// mean closing a generic definition at runtime, which Native AOT cannot do. Asking the
/// carrier costs one boxing of its default value, once per pair, and constructs nothing:
/// the adapter it names is a stateless singleton that already exists.
/// </para>
/// <para>
/// This is internal because it is how the framework pairs its own two carriers. A carrier
/// from elsewhere binds through <see cref="Attributes.ResultAdapterAttribute"/> or the
/// container's <see cref="DefaultResultAdapter"/> instead.
/// </para>
/// </remarks>
internal interface INativeAdapterCarrier
{
    /// <summary>
    /// The carrier's adapter: an <see cref="IResultAdapter{TResult}"/> closed over the
    /// carrier type itself.
    /// </summary>
    object NativeAdapter { get; }
}
