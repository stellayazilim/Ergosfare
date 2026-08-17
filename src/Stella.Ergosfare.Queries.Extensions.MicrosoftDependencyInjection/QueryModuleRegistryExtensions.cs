using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

namespace Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection;

/// <summary>
/// Adds the query module to a registry.
/// </summary>
public static class QueryModuleRegistryExtensions
{
    /// <summary>
    /// Adds the query module, registering the queries <paramref name="builder"/> selects.
    /// </summary>
    /// <param name="registry">The registry being configured.</param>
    /// <param name="builder">Selects which query constructs this container runs.</param>
    /// <returns>The same registry, so calls can be chained.</returns>
    public static IModuleRegistry AddQueryModule(this IModuleRegistry registry, Action<QueryModuleBuilder> builder)
    {
        registry.Register(new QueryModule(builder));
        return registry;
    }
}
