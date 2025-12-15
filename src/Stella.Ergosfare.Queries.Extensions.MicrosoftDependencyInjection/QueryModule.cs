using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Queries;
using Stella.Ergosfare.Queries.Abstractions;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection;

/// <summary>
/// Represents the query module which registers query types and the query mediator
/// within the dependency injection container and message registry.
/// </summary>
internal class QueryModule(
    Action<QueryModuleBuilder> builder
    ): IModule
{
    
    /// <summary>
    /// Builds and initializes the module by registering queries and the query mediator.
    /// </summary>
    /// <param name="configuration">
    /// The module configuration providing access to the service collection and message registry.
    /// </param>
    public void Build(IModuleConfiguration configuration)
    {
        builder(new QueryModuleBuilder(configuration.MessageRegistry));
        configuration.Services.TryAddTransient<IQueryMediator, QueryMediator>();
    }
}