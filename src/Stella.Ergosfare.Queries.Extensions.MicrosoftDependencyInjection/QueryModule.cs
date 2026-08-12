using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Queries.Abstractions;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection;

/// <summary>
/// Represents the query module, which selects query participants from the frozen
/// composition table and registers the query mediator with dependency injection.
/// </summary>
internal class QueryModule(
    Action<QueryModuleBuilder> builder
    ): IModule
{
    
    /// <summary>
    /// Builds and initializes the module by registering queries and the query mediator.
    /// </summary>
    /// <param name="configuration">
    /// The module configuration providing access to the service collection and this
    /// container's frozen composition selection.
    /// </param>
    public void Build(IModuleConfiguration configuration)
    {
        builder(new QueryModuleBuilder(configuration.Compositions));
        // Transient, not scoped: the mediator is stateless and a transient service is handed
        // the resolving scope's provider all the same, so per-dispatch handler resolution
        // still binds to the calling scope. Scoped would add a scope lock and a
        // resolved-services dictionary insert to every dispatch for no benefit. The
        // engine-backed shape makes the facade the only object built per resolution.
        configuration.Services.TryAddTransient<IQueryMediator, EngineBackedQueryMediator>();
    }
}
