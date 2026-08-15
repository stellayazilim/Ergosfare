using System.Collections.Immutable;

namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
///     One plugin service and how the module constructs it.
/// </summary>
/// <param name="ConstructorArguments">
///     One entry per constructor parameter: <c>null</c> binds the module's options
///     instance, anything else is a type expression resolved from the container. Default
///     when the plugin declares no options, which leaves the container to activate the
///     type as it always did.
/// </param>
/// <param name="CannotReceiveOptions">
///     The plugin declares options and this service has no way to receive them; ERGOSG017
///     reports it.
/// </param>
/// <param name="GeneratedTypeName">
///     Set when the generator writes this service's other half — the options field and its
///     constructor — into a partial declaration.
/// </param>
internal readonly record struct PluginServiceModel(
    string TypeExpression,
    string DisplayName,
    ImmutableArray<string?> ConstructorArguments = default,
    bool CannotReceiveOptions = false,
    LocationInfo? Location = null,
    string? GeneratedNamespace = null,
    string? GeneratedTypeName = null,
    string GeneratedTypeKeyword = "class")
{
    public bool Equals(PluginServiceModel other)
        => TypeExpression == other.TypeExpression
           && CannotReceiveOptions == other.CannotReceiveOptions
           && GeneratedTypeName == other.GeneratedTypeName
           && GeneratedNamespace == other.GeneratedNamespace
           && GeneratedTypeKeyword == other.GeneratedTypeKeyword
           && ConstructorArguments.SequenceEqualOrBothEmpty(other.ConstructorArguments);

    public override int GetHashCode()
        => (TypeExpression.GetHashCode() * 397) ^ (CannotReceiveOptions ? 1 : 0);
}
