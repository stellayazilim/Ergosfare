using System.Runtime.CompilerServices;


namespace Ergosfare.Contracts.Attributes;




[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class GroupAttribute(params string[] groupNames): Attribute
{
    public const string DefaultGroupName = "default";
    public string[] GroupNames => groupNames;
}