namespace Stella.Ergosfare.Contracts.Attributes;

/// <summary>
/// Specifies a weight for a class, typically used to influence the execution order or priority of handlers or modules.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class WeightAttribute(uint weight): Attribute
{
    /// <summary>
    /// Gets the weight assigned to the class.
    /// </summary>
    public uint Weight => weight;
}