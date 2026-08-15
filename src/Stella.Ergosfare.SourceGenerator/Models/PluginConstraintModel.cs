using System.Collections.Immutable;

namespace Stella.Ergosfare.SourceGenerator.Models;

/// <summary>
///     The generic constraints on a plugin method's message type parameter — the shape
///     filter of the design: the generator closes the method over each plan's concrete
///     message, so a plan whose message does not satisfy them must not get the call. Both
///     because it would be wrong and because it would not compile.
/// </summary>
/// <param name="MessageTypes">
///     The normalized type expressions the message must be assignable to; matched against
///     the message's own base-and-interface chain.
/// </param>
/// <param name="RequiresReferenceType"><c>where TMessage : class</c>.</param>
/// <param name="RequiresValueType"><c>where TMessage : struct</c>.</param>
/// <param name="IsUnmodelable">
///     A constraint the string model cannot decide — a <c>new()</c> or <c>unmanaged</c>
///     constraint, or one naming a constructed generic. Such a method is left out of every
///     plan: emitting it risks a broken consumer build, which is worse than the miss.
/// </param>
internal readonly record struct PluginConstraintModel(
    ImmutableArray<string> MessageTypes,
    bool RequiresReferenceType,
    bool RequiresValueType,
    bool IsUnmodelable)
{
    public static readonly PluginConstraintModel None =
        new(ImmutableArray<string>.Empty, false, false, false);

    public bool Equals(PluginConstraintModel other)
        => RequiresReferenceType == other.RequiresReferenceType
           && RequiresValueType == other.RequiresValueType
           && IsUnmodelable == other.IsUnmodelable
           && MessageTypes.SequenceEqualOrBothEmpty(other.MessageTypes);

    public override int GetHashCode()
    {
        var hash = RequiresReferenceType ? 1 : 0;
        hash = (hash * 397) ^ (RequiresValueType ? 1 : 0);
        hash = (hash * 397) ^ (IsUnmodelable ? 1 : 0);
        hash = (hash * 397) ^ (MessageTypes.IsDefaultOrEmpty ? 0 : MessageTypes.Length);
        return hash;
    }
}
