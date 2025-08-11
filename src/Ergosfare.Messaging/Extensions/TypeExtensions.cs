namespace Ergosfare.Messaging.Extensions;

internal static class TypeExtensions
{
    public static IEnumerable<Type> GetInterfacesEqualTo(this Type type, Type interfaceType)
    {
        return type.GetInterfaces()
            .Where(t => t.IsGenericType && t.GetGenericTypeDefinition() == interfaceType);
    }
}