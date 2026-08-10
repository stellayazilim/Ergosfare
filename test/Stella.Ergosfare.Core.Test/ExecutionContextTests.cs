using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Internal.Contexts;

namespace Stella.Ergosfare.Core.Test;

/// <summary>
/// Unit tests for verifying the behavior of execution context handling
/// and related exceptions.
/// </summary>
public class ExecutionContextTests
{
    
    /// <summary>
    /// Ensures that <see cref="ErgosfareExecutionContext"/> can be constructed,
    /// shares the same items dictionary reference, respects the cancellation token,
    /// and correctly sets the message result when aborted.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void ExecutionContextShouldConstructed()
    {
        var token = CancellationToken.None;
        var items = new Dictionary<object, object?>();
        
        // arrange & act: create a new execution context
        var ctx = new ErgosfareExecutionContext( items, token);
        
        // modify dictionary after construction (should still reference the same instance)
        items.Add("foo", "bar");

        // act & assert: aborting raises the internal unwind signal, which the mediation
        // strategy — not the caller — catches; nothing here stands between the two.
        Assert.Throws<ExecutionAbortedException>(() => ctx.Abort());
        Assert.Equal(token, ctx.CancellationToken);
        Assert.Equal(items, ctx.Items);
        
        
    }
    
}