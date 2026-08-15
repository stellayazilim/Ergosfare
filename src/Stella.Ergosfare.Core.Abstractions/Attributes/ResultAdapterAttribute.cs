using System.Diagnostics.CodeAnalysis;

namespace Stella.Ergosfare.Core.Abstractions.Attributes;

/// <summary>
/// Declares, on a message type, the <see cref="IResultAdapter{TResult}"/> that surfaces
/// value-carried failures out of the message's pipeline result — the declarative,
/// per-message binding: no runtime registration, no adapter list. The framework's own
/// <see cref="Result"/>/<see cref="Result{TValue}"/> carriers need no annotation (they
/// bind to their built-in adapters); an unannotated message with any other result type
/// performs no probing at all.
/// </summary>
/// <remarks>
/// The adapter type must implement <see cref="IResultAdapter{TResult}"/> for the message's
/// declared result type and expose a public parameterless constructor; one instance is
/// created per message type and cached. The source generator bakes the binding into the
/// message's execution plan and fails the build when the adapter does not fit the declared
/// result (planned diagnostic ERGO011).
/// </remarks>
[Experimental(ExperimentalIds.ResultAdapterSurface)]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface)]
public sealed class ResultAdapterAttribute(
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.Interfaces)]
    Type adapterType) : Attribute
{
    /// <summary>The adapter type bound to the message's pipeline result.</summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor | DynamicallyAccessedMemberTypes.Interfaces)]
    public Type AdapterType { get; } = adapterType;
}
