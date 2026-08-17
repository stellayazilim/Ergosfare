using System.Diagnostics.CodeAnalysis;
using Stella.Ergosfare.Core.Abstractions.DispatchRoots;

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
/// for one or more result types, or an open generic definition covering a family of them.
/// Which result types it serves — and, for an open definition, what closes it over each of
/// them — is worked out by the generator, which writes one already-built adapter per served
/// result type into the generated table this carrier reads. Adapters must be concrete, have
/// a public parameterless constructor, and are expected to hold no state.
/// </para>
/// <para>
/// A compilation configures one default adapter: naming a second, or naming one through
/// anything but a literal <c>typeof</c>, fails the build. That is what lets the table go
/// without a key for which adapter answered.
/// </para>
/// </remarks>
[Experimental(ExperimentalIds.ResultAdapterSurface)]
public sealed class DefaultResultAdapter
{
    /// <summary>
    /// The configured adapter type: closed, or an open generic definition.
    /// </summary>
    /// <remarks>
    /// Carried for identity — what a container reports it was configured with. Nothing
    /// closes or activates it at run time.
    /// </remarks>
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
    /// <remarks>
    /// Internal: the fallback is declared through <c>UseDefaultResultAdapter</c>, which is
    /// the call the generator reads. A carrier built any other way would name an adapter no
    /// generated table answers for.
    /// </remarks>
    internal DefaultResultAdapter(
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
    /// configured type does not serve it.
    /// </summary>
    /// <typeparam name="TResult">The result type to serve.</typeparam>
    /// <returns>The adapter, or <c>null</c>.</returns>
    /// <exception cref="InvalidOperationException">
    /// No generated adapter table was registered in this process, so the configured fallback
    /// has no answers to give.
    /// </exception>
    public IResultAdapter<TResult>? For<TResult>()
    {
        if (!GeneratedDispatchRoots.ResultAdaptersSealed)
        {
            throw new InvalidOperationException(
                $"A default result adapter ('{AdapterType}') is configured, but no generated adapter table was " +
                "registered in this process. Which result types the fallback serves — and, for an open definition, " +
                "what closes it over each of them — is decided at compile time, so an application that does not " +
                "run the Ergosfare source generator cannot use one. Reference Stella.Ergosfare's generator from " +
                "the application, or drop the UseDefaultResultAdapter call.");
        }

        return GeneratedDispatchRoots.FindDefaultResultAdapter<TResult>();
    }
}
