using Ergosfare.Core.Abstractions;
using Ergosfare.Core.Internal.Contexts;
using Microsoft.Extensions.DependencyInjection;

namespace Ergosfare.Test.Fixtures;




/// <summary>
/// Provides a dedicated execution context fixture for tests, implementing <see cref="IFixture{ExecutionContextFixture}"/> and <see cref="IAsyncDisposable"/>.
/// This fixture allows creating isolated execution scopes and exposes a default ambient context.
/// </summary>
/// <example>
/// <code>
/// // Create a new fixture
/// var fixture = new ExecutionContextFixture();
///
/// // Use a scoped context
/// await using (var scope = fixture.CreateScope())
/// {
///     var ctx = AmbientExecutionContext.Current;
///     // perform operations within the scope
/// }
///
/// // Create a completely new, empty context
/// var emptyContext = fixture.CreateContext();
///
/// // Get a fresh fixture instance for per-test isolation
/// var newFixture = fixture.New;
/// </code>
/// </example>
/// <remarks>
/// <para>
/// Use <see cref="CreateScope"/> when you want to temporarily replace the ambient execution context
/// within a limited block, ensuring that the previous context is automatically restored after disposal.
/// </para>
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
    private bool _disposed;
    private readonly IExecutionContext _executionContext = 
        new ErgosfareExecutionContext(new Dictionary<object, object?>(), CancellationToken.None);
   

    
    /// <summary>
    /// Creates a new, independent instance of <see cref="ExecutionContextFixture"/>.
    /// Useful for per-test isolation or creating multiple contexts in a single test.
    /// </summary>
    public ExecutionContextFixture New => new ExecutionContextFixture();

    
    /// <summary>
    /// Propagates the fixture's default execution context (<see cref="Ctx"/>) 
    /// to the <see cref="AmbientExecutionContext.Current"/>.
    /// </summary>
    /// <remarks>
    /// This is useful when tests require the ambient context to be explicitly set 
    /// to the fixture's default execution context rather than creating a scoped one.
    /// </remarks>
    /// <returns>
    /// The current <see cref="ExecutionContextFixture"/> instance for fluent chaining.
    /// </returns>
    /// <example>
    /// <code>
    /// var fixture = new ExecutionContextFixture();
    /// fixture.PropagateAmbientContext();
    /// 
    /// Assert.Same(fixture.Ctx, AmbientExecutionContext.Current);
    /// </code>
    /// </example>
    public ExecutionContextFixture PropagateAmbientContext()
    {
        AmbientExecutionContext.Current = Ctx;
        return this;
    }

    /// <summary>
    /// Creates a new scoped execution context for use in tests.
    /// Returns an <see cref="IAsyncDisposable"/> that restores the previous ambient context upon disposal.
    /// </summary>
    /// <returns>A disposable scope that ensures isolation of the ambient execution context.</returns>
    public IAsyncDisposable CreateScope()
    {
        var scopedCtx = new ErgosfareExecutionContext(
            new Dictionary<object, object?>(),
            _executionContext.CancellationToken);
        
        return AmbientExecutionContext.CreateScope(scopedCtx);
    }

    
    /// <summary>
    /// Creates a scoped execution context for use in tests, using the provided <paramref name="context"/>.
    /// The returned <see cref="IAsyncDisposable"/> restores the previous ambient context when disposed.
    /// </summary>
    /// <param name="context">The <see cref="IExecutionContext"/> to use for the scope.</param>
    /// <returns>A disposable scope that ensures isolation of the ambient execution context.</returns>
    /// <example>
    /// <code>
    /// var fixture = new ExecutionContextFixture();
    /// var customContext = fixture.CreateContext();
    ///
    /// await using (var scope = fixture.CreateScope(customContext))
    /// {
    ///     var ctx = AmbientExecutionContext.Current;
    ///     // ctx is the customContext you provided
    /// }
    /// </code>
    /// </example>
    /// <remarks>
    /// Use this overload when you want to run a block of code with a specific execution context
    /// rather than the fixture's default context.
    /// </remarks>
    public IAsyncDisposable CreateScope(IExecutionContext context)
    {
        return AmbientExecutionContext.CreateScope(context);
    }
    
   
    
    /// <summary>
    /// Gets the default execution context created by this fixture.
    /// </summary>
    public IExecutionContext Ctx => _executionContext;

    /// <summary>
    /// Creates a self-contained, empty <see cref="IExecutionContext"/> for tests that require a fresh context.
    /// </summary>
    /// <returns>A new instance of <see cref="ErgosfareExecutionContext"/> with no ambient state.</returns>
    public IExecutionContext CreateContext() =>
        new ErgosfareExecutionContext(new Dictionary<object, object?>(), CancellationToken.None);

    /// <summary>
    /// Disposes the fixture, resetting the ambient context if it matches the fixture's default context.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        AmbientExecutionContext.Current = null!;
        _disposed = true;
    }

    /// <summary>
    /// Asynchronously disposes the fixture. Internally calls <see cref="Dispose"/>.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> representing the asynchronous dispose operation.</returns>
    public ValueTask DisposeAsync()
    {
        Dispose();
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