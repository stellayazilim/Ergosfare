namespace Ergosfare.Contracts.Attributes;


[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class WeightAttribute(uint weight): Attribute
{
    public uint Weight => weight;
}