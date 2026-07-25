using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Test.Fixtures;



/// <summary>
/// Represents a reusable test fixture with a unified API for dependency management.
/// Provides methods for configuring services, accessing a service provider,
/// and creating fresh instances for per-test isolation.
/// </summary>
/// <typeparam name="TFixture">The concrete type of the fixture implementing this interface.</typeparam
public interface IFixture<out TFixture> : IDisposable
    where TFixture : IFixture<TFixture>
{
    /// <summary>
    /// Allows registering additional services or dependencies before the fixture is initialized.
    /// Typically used to configure the <see cref="ServiceProvider"/> before it is built.
    /// </summary>
    /// <param name="configure">The action that configures the <see cref="IServiceCollection"/>.</param>
    /// <returns>The current fixture instance for fluent chaining.</returns>
    TFixture AddServices(Action<IServiceCollection> configure);

    /// <summary>
    /// Gets the <see cref="ServiceProvider"/> built from the registered services.
    /// Use this to resolve dependencies or services required by the fixture or tests.
    /// </summary>
    ServiceProvider ServiceProvider { get; }
    
    /// <summary>
    /// Gets a fresh, independent instance of this fixture.
    /// Allows creating a new instance from an existing fixture for per-test usage,
    /// ensuring that each test can work with an isolated fixture without sharing state.
    /// </summary>
    TFixture New { get; }
}