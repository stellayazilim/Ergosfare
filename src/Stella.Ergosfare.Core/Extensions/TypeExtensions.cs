using System.Diagnostics.CodeAnalysis;
using Stella.Ergosfare.Core.Abstractions.Attributes;

namespace Stella.Ergosfare.Core.Extensions;

/// <summary>
/// Reads the pipeline attributes and generic contracts off a <see cref="Type"/>.
/// </summary>
internal static class TypeExtensions
{
    /// <summary>
    /// Returns the interfaces <paramref name="type"/> implements that close
    /// <paramref name="interfaceType"/>.
    /// </summary>
    /// <param name="type">The type to inspect.</param>
    /// <param name="interfaceType">The generic interface definition to match.</param>
    /// <returns>
    /// The matching closed interfaces; empty when the type implements none. A type
    /// implementing the same definition several times yields one entry per closing.
    /// </returns>
    public static IEnumerable<Type> GetInterfacesEqualTo(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] this Type type,
        Type interfaceType)
    {
        return type.GetInterfaces()
            .Where(t => t.IsGenericType && t.GetGenericTypeDefinition() == interfaceType);
    }


    /// <summary>
    /// Returns the weight <paramref name="type"/> declares, which decides where it runs
    /// within its stage.
    /// </summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns>The declared weight, or <c>0</c> when it declares none.</returns>
    public static uint GetWeightFromAttribute(this Type type)
        => ((WeightAttribute?)Attribute
            .GetCustomAttribute(type, typeof(WeightAttribute)))?.Weight ?? 0;

    /// <summary>
    /// Returns the groups <paramref name="type"/> declares.
    /// </summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns>
    /// The declared groups, or <see cref="GroupAttribute.DefaultGroupName"/> alone when it
    /// declares none.
    /// </returns>
    public static IReadOnlyCollection<string> GetGroupsFromAttribute(this Type type)
        => ((GroupAttribute?)Attribute
            .GetCustomAttribute(type, typeof(GroupAttribute)))?.GroupNames ?? [GroupAttribute.DefaultGroupName];
}
