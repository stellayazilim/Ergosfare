using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Plugins.Abstractions.Generated;
using Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection;

namespace Stella.Ergosfare.E2E.UseCases;

/// <summary>
///     The application layer composes the pipeline; the host only asks for it.
/// </summary>
/// <remarks>
/// This assembly exports compile-time selections for AddApplication. The API generator
/// follows that call and produces executable plans in the composition root.
/// </remarks>
public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
        => services.AddErgosfare(o => o
            .AddCommandModule(c => c.AddGenerated())
            .AddQueryModule(q => q.AddGenerated())
            .AddEventModule(e => e.AddGenerated())
            .AddTiming());
}
