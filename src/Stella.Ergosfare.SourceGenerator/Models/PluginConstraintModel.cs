using System.Collections.Immutable;

namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
/// The constraints on a plugin method's message type parameter, which are also what filters
/// the method by message shape.
/// </summary>
/// <param name="MessageTypes">
/// The types the message must be assignable to, matched against its own base types and
/// interfaces.
/// </param>
/// <param name="RequiresReferenceType">Whether the method requires a reference type.</param>
/// <param name="RequiresValueType">Whether the method requires a value type.</param>
/// <param name="IsUnmodelable">
/// Whether the method carries a constraint this model cannot decide — a <c>new()</c> or
/// <c>unmanaged</c> constraint, or one naming a constructed generic. Such a method is left
/// out of every plan: writing the call anyway risks breaking the consumer's build, which is
/// worse than missing the call.
/// </param>
/// <remarks>
/// The method is closed over each pipeline's own message type, so a message that does not
/// satisfy the constraints must not get the call — it would be wrong, and it would not
/// compile.
/// </remarks>
internal readonly record struct PluginConstraintModel(
    ImmutableArray<string> MessageTypes,
    bool RequiresReferenceType,
    bool RequiresValueType,
    bool IsUnmodelable)
{
    /// <summary>
    /// The constraints of a method that declares none, which every message satisfies.
    /// </summary>
    public static readonly PluginConstraintModel None =
        new(ImmutableArray<string>.Empty, false, false, false);

    /// <summary>
    /// Compares every constraint.
    /// </summary>
    /// <param name="other">The model to compare against.</param>
    /// <returns><c>true</c> when both admit the same messages.</returns>
    /// <remarks>
    /// Written by hand because the generated comparison would compare the type array by
    /// reference, which would defeat the incremental caching this model exists for.
    /// </remarks>
    public bool Equals(PluginConstraintModel other)
        => RequiresReferenceType == other.RequiresReferenceType
           && RequiresValueType == other.RequiresValueType
           && IsUnmodelable == other.IsUnmodelable
           && MessageTypes.SequenceEqualOrBothEmpty(other.MessageTypes);

    /// <summary>
    /// Returns a hash over the flags and the number of constrained types.
    /// </summary>
    public override int GetHashCode()
    {
        var hash = RequiresReferenceType ? 1 : 0;
        hash = (hash * 397) ^ (RequiresValueType ? 1 : 0);
        hash = (hash * 397) ^ (IsUnmodelable ? 1 : 0);
        hash = (hash * 397) ^ (MessageTypes.IsDefaultOrEmpty ? 0 : MessageTypes.Length);
        return hash;
    }
}
