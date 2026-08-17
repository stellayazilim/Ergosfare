using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Queries.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection;

/// <summary>
/// The module that registers an application's queries and the mediator that executes them.
/// </summary>
/// <param name="builder">Selects which query constructs this container runs.</param>
internal class QueryModule(
    Action<QueryModuleBuilder> builder
    ): IModule
{
    /// <summary>
    /// Runs the application's selection and registers the query mediator.
    /// </summary>
    /// <param name="configuration">The container being built and its composition selection.</param>
    public void Build(IModuleConfiguration configuration)
    {
        builder(new QueryModuleBuilder(configuration.Compositions));

        // Transient: the mediator holds nothing but the provider that resolved it, and a
        // transient still receives the calling scope's provider.
        configuration.Services.TryAddTransient<IQueryMediator, EngineBackedQueryMediator>();

        // The same facade under its concrete name, so an application can inject either and
        // a query through the class is a direct call rather than a virtual one.
        configuration.Services.TryAddTransient<QueryMediator>(
            static provider => provider.GetRequiredService<IQueryMediator>() as QueryMediator
                ?? throw new InvalidOperationException(
                    "The registered IQueryMediator is not a QueryMediator; a replacement registration cannot serve the concrete facade."));
    }
}
