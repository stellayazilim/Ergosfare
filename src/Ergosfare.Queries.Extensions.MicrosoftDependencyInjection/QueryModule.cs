
using Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Ergosfare.Queries.Abstractions;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ergosfare.Queries.Extensions.MicrosoftDependencyInjection;

internal class QueryModule(
    Action<QueryModuleBuilder> builder
    ): IModule
{
    public void Build(IModuleConfiguration configuration)
    {
        builder(new QueryModuleBuilder(configuration.MessageRegistry));
        configuration.Services.TryAddTransient<IQueryMediator, QueryMediator>();
    }
}