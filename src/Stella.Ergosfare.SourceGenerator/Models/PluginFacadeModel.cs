using System.Collections.Immutable;

namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>The plugin declaration reduced to what the facade emission needs.</summary>
internal readonly record struct PluginFacadeModel(
    string Name,
    string? OptionsTypeExpression,
    string OptionsDisplayName,
    ImmutableArray<PluginServiceModel> Services)
{
    public bool Equals(PluginFacadeModel other)
        => Name == other.Name
           && OptionsTypeExpression == other.OptionsTypeExpression
           && Services.SequenceEqualOrBothEmpty(other.Services);

    public override int GetHashCode()
        => (Name.GetHashCode() * 397) ^ Services.Length;
}
