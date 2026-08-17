using System.Diagnostics.CodeAnalysis;
using Stella.Ergosfare.Core.Abstractions.Results;

namespace Stella.Ergosfare.Core.Abstractions.Attributes;

/// <summary>
/// Binds a message type to the <see cref="IResultAdapter{TResult}"/> that reads
/// value-carried failures out of its pipeline result.
/// </summary>
/// <remarks>
/// <para>
/// The adapter type must implement <see cref="IResultAdapter{TResult}"/> for the message's
/// declared result type and expose a public parameterless constructor; one instance is
/// created per message type and reused. A message whose result is the built-in
/// <see cref="Result"/> or <see cref="Result{TValue}"/> needs no annotation — those bind to
/// their own adapters — and a message with any other result type and no annotation is never
/// probed for failures.
/// </para>
/// <para>
/// The attribute is inherited, so an annotation on a base message type covers the messages
/// derived from it. An adapter that cannot serve the declared result type fails the build
/// with ERGO011.
/// </para>
/// </remarks>
[Experimental(ExperimentalIds.ResultAdapterSurface)]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface)]
public sealed class ResultAdapterAttribute(
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.Interfaces)]
    Type adapterType) : Attribute
{
    /// <summary>
    /// The adapter type bound to this message's pipeline result.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.Interfaces)]
    public Type AdapterType { get; } = adapterType;
}
