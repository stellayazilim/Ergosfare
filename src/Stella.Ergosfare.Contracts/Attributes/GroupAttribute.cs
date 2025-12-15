namespace Stella.Ergosfare.Contracts.Attributes;


/// <summary>
/// Specifies one or more group names for a class, typically used to categorize handlers, messages, or modules.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class GroupAttribute(params string[] groupNames): Attribute
{
    /// <summary>
    /// The default group name used when no group is explicitly specified.
    /// </summary>
    public const string DefaultGroupName = "default";
    
    /// <summary>
    /// Gets the group names assigned to this class.
    /// </summary>
    public string[] GroupNames => groupNames;
}