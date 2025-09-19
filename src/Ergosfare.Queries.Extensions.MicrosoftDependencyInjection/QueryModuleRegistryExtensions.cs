using Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

namespace Ergosfare.Queries.Extensions.MicrosoftDependencyInjection;

/// <summary>
/// Provides extension methods for <see cref="IModuleRegistry"/> to add the query module.
/// </summary>
public static class QueryModuleRegistryExtensions
{
    
    /// <summary>
    /// Adds the query module to the specified module registry, allowing registration
    /// of query types and enabling the <see cref="IQueryMediator"/> for dispatching queries.
    /// </summary>
    /// <param name="registry">The module registry to which the query module will be added.</param>
    /// <param name="builder">
    /// An action that configures the query module using a <see cref="QueryModuleBuilder"/> 
    /// to register query types.
    /// </param>
    /// <returns>The <paramref name="registry"/> with the query module added, enabling fluent chaining.</returns>
    public static IModuleRegistry AddQueryModule(this IModuleRegistry registry, Action<QueryModuleBuilder> builder)
    {
        registry.Register(new QueryModule(builder));
        return registry;
    }
}