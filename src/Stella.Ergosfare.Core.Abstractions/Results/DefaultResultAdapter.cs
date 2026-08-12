using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Stella.Ergosfare.Core.Abstractions.Results;

/// <summary>
/// The application-wide fallback adapter, configured once inside <c>AddErgosfare</c> and
/// registered into the container as a normal singleton service. A result slot that binds
/// nothing more specific — no <see cref="Attributes.ResultAdapterAttribute"/> on the
/// message, not a native <see cref="Result"/>/<see cref="Result{TValue}"/> carrier —
/// falls back to this adapter when it can serve the slot; a slot it cannot serve keeps
/// the classic try/catch semantics. Nobody is forced onto the value channel: with no
/// default configured and no annotation, pipelines behave exactly as before.
/// </summary>
/// <remarks>
/// The adapter type may be a closed type implementing <see cref="IResultAdapter{TResult}"/>
/// for one or more carrier types, or an open generic definition (e.g. an adapter for a
/// foreign <c>Result&lt;T&gt;</c> family): the slot's result type is unified against the
/// definition's <see cref="IResultAdapter{TResult}"/> implementations and the definition
/// is closed accordingly. One instance is created per served result type and cached for
/// the container's lifetime, so resolution runs once per slot. Adapters are expected to
/// be stateless; a public parameterless constructor is required.
/// </remarks>
[Experimental(ExperimentalIds.ResultAdapterSurface)]
public sealed class DefaultResultAdapter
{
    private readonly ConcurrentDictionary<Type, object?> _closedAdapters = new();

    /// <summary>The configured adapter type — closed, or an open generic definition.</summary>
    /// <remarks>
    /// The annotations carry through from the constructor's parameter: closing an open
    /// generic definition over a result slot walks the type's interfaces, and activating
    /// the closed adapter needs its parameterless constructor.
    /// </remarks>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
    public Type AdapterType { get; }

    /// <summary>Wraps and validates the configured adapter type.</summary>
    /// <param name="adapterType">The adapter type; closed or an open generic definition.</param>
    /// <exception cref="ArgumentNullException"><paramref name="adapterType"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// The type implements no <see cref="IResultAdapter{TResult}"/> contract, is abstract,
    /// or lacks a public parameterless constructor.
    /// </exception>
    public DefaultResultAdapter(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.Interfaces)]
        Type adapterType)
    {
        if (adapterType is null)
        {
            throw new ArgumentNullException(nameof(adapterType));
        }

        var implementsContract = false;

        foreach (var iface in adapterType.GetInterfaces())
        {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IResultAdapter<>))
            {
                implementsContract = true;
                break;
            }
        }

        if (!implementsContract)
        {
            throw new ArgumentException(
                $"'{adapterType}' cannot serve as the default result adapter: it implements no IResultAdapter<TResult> contract.",
                nameof(adapterType));
        }

        if (adapterType.IsAbstract || adapterType.GetConstructor(Type.EmptyTypes) is null)
        {
            throw new ArgumentException(
                $"'{adapterType}' cannot serve as the default result adapter: a concrete type with a public parameterless constructor is required.",
                nameof(adapterType));
        }

        AdapterType = adapterType;
    }

    /// <summary>
    /// The adapter serving the given result slot, or <c>null</c> when the configured type
    /// cannot serve it — resolved once per slot and cached.
    /// </summary>
    public IResultAdapter<TResult>? For<TResult>()
        => (IResultAdapter<TResult>?)_closedAdapters.GetOrAdd(typeof(TResult), static (resultType, self) => self.Close(resultType), this);

    [UnconditionalSuppressMessage("Trimming", "IL2055",
        Justification = "The definition is closed over a live pipeline's result payload type; the pipeline roots it.")]
    [UnconditionalSuppressMessage("Trimming", "IL2072",
        Justification = "The constructor requires and the attribute annotation preserves the adapter's public parameterless constructor.")]
    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "Open-generic default adapters close over result types outside generated dispatch roots; " +
                        "generated apps close the executor generics — and with them these slots — at compile time.")]
    private object? Close(Type resultType)
    {
        if (!AdapterType.IsGenericTypeDefinition)
        {
            return typeof(IResultAdapter<>).MakeGenericType(resultType).IsAssignableFrom(AdapterType)
                ? Activator.CreateInstance(AdapterType)
                : null;
        }

        foreach (var iface in AdapterType.GetInterfaces())
        {
            if (!iface.IsGenericType || iface.GetGenericTypeDefinition() != typeof(IResultAdapter<>))
            {
                continue;
            }

            var arguments = new Type?[AdapterType.GetGenericArguments().Length];

            if (!TryUnify(iface.GetGenericArguments()[0], resultType, arguments) || Array.IndexOf(arguments, null) >= 0)
            {
                continue;
            }

            try
            {
                return Activator.CreateInstance(AdapterType.MakeGenericType(arguments!));
            }
            catch (ArgumentException)
            {
                // A generic constraint rejected the closing — this implementation cannot
                // serve the slot; another of the definition's contracts still might.
            }
        }

        return null;
    }

    /// <summary>
    /// Unifies the definition's declared carrier pattern with a concrete result type,
    /// binding the definition's type parameters by position. Structural and one adapter
    /// parameter per position: a parameter bound twice must bind identically.
    /// </summary>
    private static bool TryUnify(Type pattern, Type concrete, Type?[] arguments)
    {
        if (pattern.IsGenericParameter)
        {
            var position = pattern.GenericParameterPosition;

            if (arguments[position] is { } bound)
            {
                return bound == concrete;
            }

            arguments[position] = concrete;
            return true;
        }

        if (pattern.IsGenericType)
        {
            if (!concrete.IsGenericType || pattern.GetGenericTypeDefinition() != concrete.GetGenericTypeDefinition())
            {
                return false;
            }

            var patternArguments = pattern.GetGenericArguments();
            var concreteArguments = concrete.GetGenericArguments();

            for (var i = 0; i < patternArguments.Length; i++)
            {
                if (!TryUnify(patternArguments[i], concreteArguments[i], arguments))
                {
                    return false;
                }
            }

            return true;
        }

        return pattern == concrete;
    }
}
