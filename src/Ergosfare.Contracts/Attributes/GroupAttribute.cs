namespace Ergosfare.Contracts.Attributes;


[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public class GroupAttribute(params string[] groupNames): Attribute
{
    public string[] GroupNames => groupNames;
}