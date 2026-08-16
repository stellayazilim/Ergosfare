namespace Stella.Ergosfare.Core.Abstractions.Results;

/// <summary>
/// Lets the framework's own result carriers name their built-in adapter from inside a generic
/// context, so the pairing never has to be reconstructed reflectively.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ResultAdapterBinding"/> resolves a pair as <c>Slot&lt;TMessage, TResult&gt;</c>.
/// <c>TResult</c> is the closed carrier, but the carrier's payload type is not a type parameter
/// of the slot — so reading <see cref="ResultExceptionAdapter{TValue}"/> for it used to mean
/// closing that definition with <see cref="System.Type.MakeGenericType"/> and reading its
/// <c>Instance</c> field reflectively. That is an answer only a JIT can give: the binding worked
/// in development and had nothing to close under Native AOT, the same publish-time divergence
/// the dispatch fallback carries.
/// </para>
/// <para>
/// Asking the carrier instead costs one boxing of <c>default(TResult)</c>, in the slot's static
/// constructor, once per closed pair. Nothing is constructed: the carrier exists, so its closed
/// generic and its interface implementation were already emitted, and the adapter it names is
/// the same stateless singleton either way.
/// </para>
/// <para>
/// Internal on purpose. Which adapter serves a carrier is the framework's own pairing for its
/// own two carriers; a foreign carrier binds through
/// <see cref="Attributes.ResultAdapterAttribute"/> or the container's
/// <see cref="DefaultResultAdapter"/>, neither of which this contract participates in.
/// </para>
/// </remarks>
internal interface INativeAdapterCarrier
{
    /// <summary>
    /// The carrier's built-in adapter — an <see cref="IResultAdapter{TResult}"/> closed over the
    /// carrier itself, so the binding's cast to the slot's own contract always succeeds.
    /// </summary>
    object NativeAdapter { get; }
}
