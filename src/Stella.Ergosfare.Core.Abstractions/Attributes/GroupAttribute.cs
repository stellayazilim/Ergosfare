
namespace Stella.Ergosfare.Core.Abstractions.Attributes;


/// <summary>
/// Assigns a participant to one or more pipeline groups, so a dispatch can select which
/// participants run.
/// </summary>
/// <remarks>
/// A participant without this attribute belongs to <see cref="DefaultGroupName"/> alone.
/// A dispatch that requests no groups runs the default group; a dispatch that requests
/// group names runs every participant declaring at least one of them, compared ordinally.
/// Declaring groups therefore takes a participant out of the default group unless it
/// lists <see cref="DefaultGroupName"/> explicitly.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class GroupAttribute(params string[] groupNames): Attribute
{
    /// <summary>
    /// The group a participant belongs to when it declares none, and the group a dispatch
    /// runs when it requests none.
    /// </summary>
    public const string DefaultGroupName = "default";

    /// <summary>
    /// The group names declared for this participant.
    /// </summary>
    public string[] GroupNames => groupNames;
}
