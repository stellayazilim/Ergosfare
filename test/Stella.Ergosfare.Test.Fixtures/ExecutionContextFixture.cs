using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Internal.Contexts;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Test.Fixtures;




/// <summary>
/// Provides a dedicated execution context fixture for tests, implementing <see cref="IFixture{ExecutionContextFixture}"/> and <see cref="IAsyncDisposable"/>.
/// </summary>
/// <remarks>
/// <para>
/// Use <see cref="CreateContext"/> when you need a completely isolated <see cref="IExecutionContext"/>
/// that is independent of the fixture’s default context and has no pre-existing state.
/// </para>
/// <para>
/// Access <see cref="New"/> when you need a fresh fixture instance for per-test isolation,
/// preventing shared state between tests.
/// </para>
/// </remarks>
public class ExecutionContextFixture : IAsyncDisposable, IFixture<ExecutionContextFixture>
{
    private readonly IExecutionContext _executionContext =
        new ErgosfareExecutionContext( new Dictionary<object, object?>(), CancellationToken.None);



    /// <summary>
    /// Creates a new, independent instance of <see cref="ExecutionContextFixture"/>.
    /// Useful for per-test isolation or creating multiple contexts in a single test.
    /// </summary>
    public ExecutionContextFixture New => new ExecutionContextFixture();

    /// <summary>
    /// Gets the default execution context created by this fixture.
    /// </summary>
    public IExecutionContext Ctx => _executionContext;

    /// <summary>
    /// Creates a self-contained, empty <see cref="IExecutionContext"/> for tests that require a fresh context.
    /// </summary>
    /// <returns>A new instance of <see cref="ErgosfareExecutionContext"/> with no ambient state.</returns>
    public IExecutionContext CreateContext() =>
        new ErgosfareExecutionContext(  new Dictionary<object, object?>(), CancellationToken.None);

    /// <summary>
    /// Disposes the fixture. The fixture holds no disposable state.
    /// </summary>
    public void Dispose()
    {
    }

    /// <summary>
    /// Asynchronously disposes the fixture. The fixture holds no disposable state.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous dispose operation.</returns>
    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
    
    
    #region IFixture Implementation
    
    /// <summary>
    /// Allows adding services to the fixture for consistency with <see cref="IFixture{ExecutionContextFixture}"/>.
    /// This fixture does not interact with DI, so this method is a no-op.
    /// </summary>
    /// <param name="configure">The service configuration action.</param>
    /// <returns>The current fixture instance for fluent chaining.</returns>
    public ExecutionContextFixture AddServices(Action<IServiceCollection> configure)
    {
        // No-op since this fixture does not provide DI
        return this;
    }
    
    /// <summary>
    /// Returns the <see cref="ServiceProvider"/> for this fixture.
    /// This fixture does not provide DI services, so it returns null.
    /// </summary>
    public ServiceProvider ServiceProvider { get; } = null!;
    
    #endregion
}