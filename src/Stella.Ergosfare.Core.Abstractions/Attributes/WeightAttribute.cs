
namespace Stella.Ergosfare.Core.Abstractions.Attributes;

/// <summary>
/// Sets the invocation order of a participant within its pipeline stage.
/// </summary>
/// <remarks>
/// Participants in a stage run by descending weight, and participants of equal weight run
/// in ordinal order of their full type name, so ordering is stable across runs. A
/// participant without this attribute has weight zero.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class WeightAttribute(uint weight): Attribute
{
    /// <summary>
    /// The weight declared for this participant; higher runs earlier.
    /// </summary>
    public uint Weight => weight;
}
