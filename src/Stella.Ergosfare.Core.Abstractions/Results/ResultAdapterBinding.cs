// The binding is the runtime mirror of the experimental annotation surface — it reads
// the marked attributes and consults the marked default-adapter carrier by design.
#pragma warning disable ERGOEXP001

using System.Diagnostics.CodeAnalysis;
using Stella.Ergosfare.Core.Abstractions.Attributes;

namespace Stella.Ergosfare.Core.Abstractions.Results;

/// <summary>
/// Resolves the result adapter of a (message, result-slot) pair. Resolution ladder:
/// <see cref="IgnoreResultAdapterAttribute"/> opts the message out entirely; else the
/// message's <see cref="ResultAdapterAttribute"/> when its adapter fits the slot exactly;
/// else the built-in adapters of the framework's own
/// <see cref="Result"/>/<see cref="Result{TValue}"/> carriers; else — on the
/// provider-taking overload — the container's configured <see cref="DefaultResultAdapter"/>
/// when it can serve the slot; else <c>null</c>, the overwhelmingly common case, in which
/// the pipeline performs no probing at all and keeps the classic try/catch semantics.
/// Nobody is forced onto the value channel — adapters are a recommended win, never a
/// requirement.
/// </summary>
/// <remarks>
/// The attribute tiers resolve once per closed pair into a static generic slot, so
/// dispatch paths only ever read a field; the default tier is per-container and cached by
/// the callers that consult it. Both attributes are inherited; declaring both on one
/// message (own or inherited) fails the build (ERGOSG012) — against assemblies compiled
/// before that rule, the opt-out wins here. This is the runtime mirror of a compile-time
/// fact: the source generator bakes the same attribute-tier binding into execution plans
/// and fails the build on a mismatched annotation (ERGOSG011).
/// </remarks>
public static class ResultAdapterBinding
{
    /// <summary>
    /// The attribute-tier adapter bound to the pair — annotation, then native — or
    /// <c>null</c> when neither binds or the message opts out. Does not consult the
    /// container's default adapter; dispatch paths use the provider-taking overload.
    /// </summary>
    public static IResultAdapter<TResult>? For<TMessage, TResult>() => Slot<TMessage, TResult>.Adapter;

    /// <summary>
    /// The effective adapter of the pair: the attribute tiers first, then the container's
    /// configured <see cref="DefaultResultAdapter"/> when it can serve the slot. A message
    /// carrying <see cref="IgnoreResultAdapterAttribute"/> resolves to <c>null</c> past
    /// every tier, the default included.
    /// </summary>
    /// <param name="serviceProvider">The container the dispatch runs in; supplies the configured default adapter, if any.</param>
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

    private static class Slot<TMessage, TResult>
    {
        public static readonly IResultAdapter<TResult>? Adapter;
        public static readonly bool Ignored;

        static Slot()
        {
            (Ignored, Adapter) = Resolve();
        }

        [UnconditionalSuppressMessage("Trimming", "IL2055",
            Justification = "The built-in adapter generic closes over a live pipeline's result payload type; the pipeline roots it.")]
        [UnconditionalSuppressMessage("Trimming", "IL2072",
            Justification = "The annotation's DynamicallyAccessedMembers preserves the adapter's public parameterless constructor.")]
        [UnconditionalSuppressMessage("AOT", "IL3050",
            Justification = "Reflective instantiation only serves Result<T> pipelines outside generated dispatch roots; " +
                            "generated apps close the executor generics — and with them this slot — at compile time.")]
        private static (bool Ignored, IResultAdapter<TResult>? Adapter) Resolve()
        {
            // One walk over the base chain for both attributes — the runtime mirror of
            // GetCustomAttribute's inheritance. The opt-out wins over an annotation from
            // any level: in-source the combination is ERGOSG012 and never compiles, so
            // this arbitration only ever serves assemblies compiled before that rule.
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
                // Exact-slot fit only: the annotation targets the declared result; other
                // slots of the same message resolve past it.
                if (typeof(IResultAdapter<TResult>).IsAssignableFrom(annotation.AdapterType))
                {
                    return (false, (IResultAdapter<TResult>)Activator.CreateInstance(annotation.AdapterType)!);
                }
            }

            var resultType = typeof(TResult);

            if (resultType == typeof(Result))
            {
                return (false, (IResultAdapter<TResult>)(object)ResultExceptionAdapter.Instance);
            }

            if (resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(Result<>))
            {
                var adapterType = typeof(ResultExceptionAdapter<>).MakeGenericType(resultType.GetGenericArguments());

                return (false, (IResultAdapter<TResult>)adapterType.GetField("Instance")!.GetValue(null)!);
            }

            return (false, null);
        }
    }
}
