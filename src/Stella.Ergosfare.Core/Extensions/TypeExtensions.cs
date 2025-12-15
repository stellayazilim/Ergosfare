using Stella.Ergosfare.Contracts.Attributes;

namespace Stella.Ergosfare.Core.Extensions;

/// <summary>
/// Provides extension methods for <see cref="Type"/> to inspect attributes and generic interfaces.
/// </summary>
internal static class TypeExtensions
{
    /// <summary>
    /// Gets all interfaces implemented by the specified type that match the given generic interface type definition.
    /// </summary>
    /// <param name="type">The type to inspect.</param>
    /// <param name="interfaceType">The generic interface type definition to match.</param>
    /// <returns>An enumerable of interfaces implemented by <paramref name="type"/> that match <paramref name="interfaceType"/>.</returns>
    public static IEnumerable<Type> GetInterfacesEqualTo(this Type type, Type interfaceType)
    {
        return type.GetInterfaces()
            .Where(t => t.IsGenericType && t.GetGenericTypeDefinition() == interfaceType);
    }
    
    
    /// <summary>
    /// Gets the weight defined by the <see cref="WeightAttribute"/> on the type, or 0 if not present.
    /// </summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns>The weight value defined by <see cref="WeightAttribute"/> or 0 if absent.</returns>
    public static uint GetWeightFromAttribute(this Type type)
        => ((WeightAttribute?)Attribute
            .GetCustomAttribute(type, typeof(WeightAttribute)))?.Weight ?? 0;

    /// <summary>
    /// Gets the groups defined by the <see cref="GroupAttribute"/> on the type,
    /// or a collection containing <see cref="GroupAttribute.DefaultGroupName"/> if not present.
    /// </summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns>A read-only collection of group names defined on the type.</returns>
    public static IReadOnlyCollection<string> GetGroupsFromAttribute(this Type type)
        => ((GroupAttribute?)Attribute
            .GetCustomAttribute(type, typeof(GroupAttribute)))?.GroupNames ?? [GroupAttribute.DefaultGroupName];
}