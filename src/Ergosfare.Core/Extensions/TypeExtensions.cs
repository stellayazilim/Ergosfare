using Ergosfare.Contracts.Attributes;

namespace Ergosfare.Core.Extensions;

internal static class TypeExtensions
{
    
    public static IEnumerable<Type> GetInterfacesEqualTo(this Type type, Type interfaceType)
    {
        return type.GetInterfaces()
            .Where(t => t.IsGenericType && t.GetGenericTypeDefinition() == interfaceType);
    }
    
    
    
    public static uint GetWeightFromAttribute(this Type type)
        => ((WeightAttribute?)Attribute
            .GetCustomAttribute(type, typeof(WeightAttribute)))?.Weight ?? 0;


    public static IReadOnlyCollection<string> GetGroupsFromAttribute(this Type type)
        => ((GroupAttribute?)Attribute
            .GetCustomAttribute(type, typeof(GroupAttribute)))?.GroupNames ?? [GroupAttribute.DefaultGroupName];
}