using Stella.MinimalApi;

namespace Stella.Ergosfare.E2E.Api.Endpoints;

/// <summary>
/// The compile-time endpoint registry: <c>Stella.MinimalApi</c>'s generator fills
/// <see cref="RegisterGenerated"/> with one explicit registration per <see cref="IEndpoint"/>
/// in this assembly, so the published binary needs no assembly scanning to find its routes.
/// </summary>
[EndpointDiscovery]
public partial class GeneratedEndpointDiscovery : IEndpointDiscovery
{
    public void Register(IServiceCollection services) => RegisterGenerated(services);

    private partial void RegisterGenerated(IServiceCollection services);
}
