using System.Collections.Immutable;

namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
/// A plugin declaration, reduced to what writing its facade needs.
/// </summary>
/// <param name="Name">The plugin's name, which the generated <c>Add&lt;Name&gt;</c> takes.</param>
/// <param name="OptionsTypeExpression">
/// The fully qualified options type, or <c>null</c> when the plugin takes none.
/// </param>
/// <param name="OptionsDisplayName">The options type as a diagnostic would name it.</param>
/// <param name="Services">The plugin's services, with the methods to call on them.</param>
internal readonly record struct PluginFacadeModel(
    string Name,
    string? OptionsTypeExpression,
    string OptionsDisplayName,
    ImmutableArray<PluginServiceModel> Services)
{
    /// <summary>
    /// Compares by name, options type and services.
    /// </summary>
    /// <param name="other">The model to compare against.</param>
    /// <returns><c>true</c> when both describe the same facade.</returns>
    /// <remarks>
    /// The display name is left out: it is derived from the options type and so cannot
    /// differ on its own. Written by hand because the generated comparison would compare the
    /// service array by reference, which would defeat the incremental caching this model
    /// exists for.
    /// </remarks>
    public bool Equals(PluginFacadeModel other)
        => Name == other.Name
           && OptionsTypeExpression == other.OptionsTypeExpression
           && Services.SequenceEqualOrBothEmpty(other.Services);

    /// <summary>
    /// Returns a hash over the name and the number of services.
    /// </summary>
    public override int GetHashCode()
        => (Name.GetHashCode() * 397) ^ Services.Length;
}
