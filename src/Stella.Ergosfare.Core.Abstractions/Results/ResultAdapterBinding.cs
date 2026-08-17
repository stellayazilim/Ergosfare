// The binding names the experimental annotation attributes and the experimental
// default-adapter carrier, which is what it exists to do.
#pragma warning disable ERGOEXP001

using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Abstractions.DispatchRoots;

namespace Stella.Ergosfare.Core.Abstractions.Results;

/// <summary>
/// Finds the result adapter for a (message, result) pair.
/// </summary>
/// <remarks>
/// <para>
/// Tiers are consulted in order: <see cref="IgnoreResultAdapterAttribute"/> on the message
/// ends the search with no adapter; then the message's
/// <see cref="ResultAdapterAttribute"/>, if its adapter serves this exact result type; then
/// the built-in adapters of <see cref="Result"/> and <see cref="Result{TValue}"/>; then —
/// on the overload that takes a provider — the container's
/// <see cref="DefaultResultAdapter"/>, if it can serve the result type. Most pairs reach
/// the end and bind nothing, which means the pipeline never inspects its result and
/// failures are thrown as usual.
/// </para>
/// <para>
/// Neither attribute is read here. Both are inherited, both are resolved by the generator
/// over the same base chain, and what reaches this class is the generated table: one entry
/// per (message, result) slot an annotation binds, and one per message that opts out. So a
/// tier decision costs a dictionary read, the adapter arrives already built and already
/// typed as the slot's contract, and nothing on this path closes a generic or activates a
/// type. A message carrying both annotations fails the build with ERGO012; where both
/// somehow reach the runtime, the opt-out wins, because the generator writes the opt-out and
/// no slot entry.
/// </para>
/// </remarks>
public static class ResultAdapterBinding
{
    /// <summary>
    /// Returns the adapter bound by the message's annotations or by a built-in carrier, or
    /// <c>null</c> when neither binds or the message opts out.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <returns>The bound adapter, or <c>null</c>.</returns>
    /// <remarks>
    /// This overload does not consult the container's default adapter; dispatch paths use
    /// the overload that takes a provider.
    /// </remarks>
    public static IResultAdapter<TResult>? For<TMessage, TResult>() => Slot<TMessage, TResult>.Adapter;

    /// <summary>
    /// Returns the adapter in effect for the pair, falling back to the container's
    /// <see cref="DefaultResultAdapter"/>.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="serviceProvider">
    /// The container the dispatch runs in, which supplies the default adapter if one is
    /// configured.
    /// </param>
    /// <returns>
    /// The adapter, or <c>null</c>. A message carrying
    /// <see cref="IgnoreResultAdapterAttribute"/> returns <c>null</c> past every tier,
    /// including the default.
    /// </returns>
    public static IResultAdapter<TResult>? For<TMessage, TResult>(IServiceProvider serviceProvider)
    {
        if (Slot<TMessage, TResult>.Ignored)
        {
            return null;
        }

        if (Slot<TMessage, TResult>.Adapter is { } bound)
        {
            return bound;
        }

        return (serviceProvider.GetService(typeof(DefaultResultAdapter)) as DefaultResultAdapter)?.For<TResult>();
    }

    /// <summary>
    /// Holds the annotation-tier binding of one closed pair, resolved on first use.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    private static class Slot<TMessage, TResult>
    {
        /// <summary>
        /// The adapter the annotation tier or a built-in carrier bound, or <c>null</c>.
        /// </summary>
        public static readonly IResultAdapter<TResult>? Adapter;

        /// <summary>
        /// Whether the message opts out of result adaptation entirely.
        /// </summary>
        // ReSharper disable once StaticMemberInGenericType
        public static readonly bool Ignored;

        static Slot()
        {
            (Ignored, Adapter) = Resolve();
        }

        /// <summary>
        /// Reads the pair's generated entries, then the result type's built-in binding.
        /// </summary>
        /// <returns>Whether the message opts out, and the adapter bound if it does not.</returns>
        private static (bool Ignored, IResultAdapter<TResult>? Adapter) Resolve()
        {
            if (GeneratedDispatchRoots.IsResultAdapterIgnored<TMessage>())
            {
                return (true, null);
            }

            // The generator wrote one entry per slot the annotated adapter fits, so a miss is
            // the message's other result types falling through to the tier below.
            if (GeneratedDispatchRoots.FindResultAdapter<TMessage, TResult>() is { } annotated)
            {
                return (false, annotated);
            }

            // The built-in carriers name their own adapter, so both are served by one
            // boxing of the result type's default value rather than a test per carrier. A
            // reference-typed result boxes to null and falls through, which is the common
            // case.
            if (default(TResult) is INativeAdapterCarrier carrier)
            {
                return (false, (IResultAdapter<TResult>)carrier.NativeAdapter);
            }

            return (false, null);
        }
    }
}
