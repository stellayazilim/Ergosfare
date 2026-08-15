using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Generated;
using Stella.Ergosfare.Plugins.Abstractions.Generated;
using Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection;

namespace Stella.Ergosfare.E2E.UseCases;

/// <summary>
///     The application layer composes the pipeline; the host only asks for it.
/// </summary>
/// <remarks>
///     <para>
///         This is the layering most applications actually have, and it is here deliberately:
///         the Ergosfare dependency — including the generator — lives in the application layer,
///         and the executable reaches it transitively. `RegisterGenerated()` is emitted into
///         <i>this</i> compilation, so the call has to live here too.
///     </para>
///     <para>
///         The consequence is the point of the arrangement. A source generator sees its own
///         compilation and its references, never its dependents, so the dispatch sites in the
///         host are invisible from here — no whole-closure reachability judgment covers them,
///         and a grouped dispatch written up there gets no group-keyed plan.
///     </para>
/// </remarks>
public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
        => services.AddErgosfare(o => o
            .AddCommandModule(c => c.RegisterGenerated())
            .AddQueryModule(q => q.RegisterGenerated())
            .AddEventModule(e => e.RegisterGenerated())
            .AddTiming());
}
