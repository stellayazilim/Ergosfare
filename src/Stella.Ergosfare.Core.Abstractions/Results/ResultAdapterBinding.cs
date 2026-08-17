// The binding reads the experimental annotation attributes and the experimental
// default-adapter carrier, which is what it exists to do.
#pragma warning disable ERGOEXP001

using System.Diagnostics.CodeAnalysis;
using Stella.Ergosfare.Core.Abstractions.Attributes;

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
/// The attribute tiers resolve once per closed pair; the default tier is resolved per
/// container by whoever consults it. Both attributes are inherited, and a message carrying
/// both fails the build with ERGO012 — where both reach the runtime anyway, the opt-out
/// wins.
/// </para>
/// </remarks>
public static class ResultAdapterBinding
{
    /// <summary>
    /// Returns the adapter bound by the message's attributes or by a built-in carrier, or
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
    /// Holds the attribute-tier binding of one closed pair, resolved on first use.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <typeparam name="TResult">The result type.</typeparam>
    private static class Slot<TMessage, TResult>
    {
        /// <summary>
        /// The adapter the attribute tiers bound, or <c>null</c>.
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
        /// Walks the message's attributes and the result type's built-in binding.
        /// </summary>
        /// <returns>Whether the message opts out, and the adapter bound if it does not.</returns>
        [UnconditionalSuppressMessage("Trimming", "IL2072",
            Justification = "The annotation's DynamicallyAccessedMembers preserves the adapter's public parameterless constructor.")]
        private static (bool Ignored, IResultAdapter<TResult>? Adapter) Resolve()
        {
            // Both attributes are collected in one walk up the base chain, which is what
            // makes them inherited. The opt-out wins over an annotation at any level; in
            // source the two together do not compile, so this only arbitrates for
            // assemblies built before that rule existed.
            var ignored = false;
            ResultAdapterAttribute? annotation = null;

            for (var current = typeof(TMessage); current is not null; current = current.BaseType)
            {
                foreach (var attribute in current.GetCustomAttributes(inherit: false))
                {
                    switch (attribute)
                    {
                        case IgnoreResultAdapterAttribute:
                            ignored = true;
                            break;
                        case ResultAdapterAttribute resultAdapter:
                            annotation ??= resultAdapter;
                            break;
                    }
                }
            }

            if (ignored)
            {
                return (true, null);
            }

            if (annotation is not null)
            {
                // The annotation binds this result type only. The message's other result
                // types fall through to the tiers below.
                if (typeof(IResultAdapter<TResult>).IsAssignableFrom(annotation.AdapterType))
                {
                    return (false, (IResultAdapter<TResult>)Activator.CreateInstance(annotation.AdapterType)!);
                }
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
