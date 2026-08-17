using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Stella.Ergosfare.Core.Abstractions.Results;

/// <summary>
/// The adapter an application falls back to for result types that bind nothing more
/// specific. Configured once in <c>AddErgosfare</c> and registered as a container
/// singleton.
/// </summary>
/// <remarks>
/// <para>
/// It applies only where nothing else does: after the message's own
/// <see cref="Attributes.ResultAdapterAttribute"/> and after the built-in
/// <see cref="Result"/> and <see cref="Result{TValue}"/> carriers. A result type it cannot
/// serve keeps the default behavior — failures are thrown rather than returned — and with
/// no default configured, nothing changes for any pipeline.
/// </para>
/// <para>
/// The configured type may be closed, implementing <see cref="IResultAdapter{TResult}"/>
/// for one or more result types, or an open generic definition covering a family of them;
/// an open definition is closed by matching a result type against its
/// <see cref="IResultAdapter{TResult}"/> implementations. One instance is created per
/// result type served and kept for the container's lifetime. Adapters must be concrete,
/// have a public parameterless constructor, and are expected to hold no state.
/// </para>
/// </remarks>
[Experimental(ExperimentalIds.ResultAdapterSurface)]
public sealed class DefaultResultAdapter
{
    private readonly ConcurrentDictionary<Type, object?> _closedAdapters = new();

    /// <summary>
    /// The configured adapter type: closed, or an open generic definition.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces | DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
    public Type AdapterType { get; }

    /// <summary>
    /// Validates <paramref name="adapterType"/> and takes it as the application's fallback
    /// adapter.
    /// </summary>
    /// <param name="adapterType">The adapter type: closed, or an open generic definition.</param>
    /// <exception cref="ArgumentNullException"><paramref name="adapterType"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    /// The type implements no <see cref="IResultAdapter{TResult}"/> contract, is abstract,
    /// or has no public parameterless constructor.
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
    /// Returns the adapter for <typeparamref name="TResult"/>, or <c>null</c> when the
    /// configured type cannot serve it. Resolved once per result type and cached.
    /// </summary>
    /// <typeparam name="TResult">The result type to serve.</typeparam>
    /// <returns>The adapter, or <c>null</c>.</returns>
    public IResultAdapter<TResult>? For<TResult>()
        => (IResultAdapter<TResult>?)_closedAdapters.GetOrAdd(typeof(TResult), static (resultType, self) => self.Close(resultType), this);

    /// <summary>
    /// Builds the adapter instance serving <paramref name="resultType"/>, or returns
    /// <c>null</c> when the configured type cannot serve it.
    /// </summary>
    /// <param name="resultType">The result type to serve.</param>
    /// <returns>The adapter instance, or <c>null</c>.</returns>
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
                // A generic constraint refused the closing. This implementation cannot serve
                // the result type, but another of the definition's contracts still might.
            }
        }

        return null;
    }

    /// <summary>
    /// Matches the result type an adapter implementation declares against a concrete result
    /// type, binding the definition's type parameters by position.
    /// </summary>
    /// <param name="pattern">The declared result type, which may contain type parameters.</param>
    /// <param name="concrete">The concrete result type to match against.</param>
    /// <param name="arguments">
    /// The bindings collected so far, indexed by parameter position; filled in as the match
    /// proceeds.
    /// </param>
    /// <returns>
    /// <c>true</c> when the two match. A parameter appearing more than once must bind to
    /// the same type each time.
    /// </returns>
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
