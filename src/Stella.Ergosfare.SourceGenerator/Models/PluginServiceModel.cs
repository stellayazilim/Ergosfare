using System.Collections.Immutable;

namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
/// One plugin service and how the generated module constructs it.
/// </summary>
/// <param name="TypeExpression">The service's fully qualified type.</param>
/// <param name="DisplayName">The service as a diagnostic would name it.</param>
/// <param name="ConstructorArguments">
/// One entry per constructor parameter: <c>null</c> means the module's options instance,
/// and anything else is a type to resolve from the container. Left unset when the plugin
/// declares no options, which leaves the container to activate the service itself.
/// </param>
/// <param name="CannotReceiveOptions">
/// Whether the plugin declares options that this service has no way to receive, which
/// ERGO017 reports.
/// </param>
/// <param name="Location">Where the service is declared, for diagnostics.</param>
/// <param name="GeneratedNamespace">
/// The namespace to write the service's generated half into, when there is one.
/// </param>
/// <param name="GeneratedTypeName">
/// The service's name, set when the generator writes its other half — the options field and
/// the constructor assigning it — into a partial declaration.
/// </param>
/// <param name="GeneratedTypeKeyword">
/// The keyword to declare that generated half with, so it matches how the service was
/// written.
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
    /// <summary>
    /// Compares everything that changes the generated output.
    /// </summary>
    /// <param name="other">The model to compare against.</param>
    /// <returns><c>true</c> when both would generate the same thing.</returns>
    /// <remarks>
    /// The display name and location are left out: they only affect diagnostic text, and
    /// including them would make the incremental pipeline regenerate for a moved line.
    /// Written by hand because the generated comparison would compare the argument array by
    /// reference.
    /// </remarks>
    public bool Equals(PluginServiceModel other)
        => TypeExpression == other.TypeExpression
           && CannotReceiveOptions == other.CannotReceiveOptions
           && GeneratedTypeName == other.GeneratedTypeName
           && GeneratedNamespace == other.GeneratedNamespace
           && GeneratedTypeKeyword == other.GeneratedTypeKeyword
           && ConstructorArguments.SequenceEqualOrBothEmpty(other.ConstructorArguments);

    /// <summary>
    /// Returns a hash over the service's type and whether it can receive options.
    /// </summary>
    public override int GetHashCode()
        => (TypeExpression.GetHashCode() * 397) ^ (CannotReceiveOptions ? 1 : 0);
}
