using Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

namespace Ergosfare.Queries.Extensions.MicrosoftDependencyInjection;
public static class QueryModuleRegistryExtensions
{
    public static IModuleRegistry AddQueryModule(this IModuleRegistry registry, Action<QueryModuleBuilder> builder)
    {
        registry.Register(new QueryModule(builder));
        return registry;
    }
}