using Stella.Ergosfare.Test.Fixtures;
using Stella.Ergosfare.Test.Fixtures.Stubs.Basic;

namespace Stella.Ergosfare.Core.Test.Abstractions.Handler;


/// <summary>
/// Base class for handler tests providing access to <see cref="ExecutionContextFixture"/>.
/// Avoids repeated constructor boilerplate in derived test classes.
/// </summary>
public abstract class BaseHandlerFixture
    : IClassFixture<ExecutionContextFixture>
{
    /// <summary>
    /// The shared execution context fixture.
    /// </summary>
    protected readonly ExecutionContextFixture ExecutionContextFixture;
    
    
    /// <summary>
    /// A default <see cref="StubMessage"/> instance for test inputs.
    /// </summary>
    protected readonly StubMessage Message = new ();
    
    /// <summary>
    /// Initializes a new instance of <see cref="BaseHandlerFixture"/>.
    /// </summary>
    /// <param name="executionContextFixture">The execution context fixture provided by xUnit.</param>
    protected BaseHandlerFixture(ExecutionContextFixture executionContextFixture)
    {
        ExecutionContextFixture = executionContextFixture;
    }
}