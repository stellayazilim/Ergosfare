using Ergosfare.Core.Abstractions.Handlers;
using Ergosfare.Test.Fixtures;
using Ergosfare.Test.Fixtures.Stubs.Basic;

namespace Ergosfare.Core.Test.Abstractions.Handler;


/// <summary>
/// Unit tests to verify that <see cref="IHandler"/> implementations behave correctly
/// when invoked through the non-generic interface.
/// </summary>
/// <remarks>
/// These tests ensure that handlers returning <c>void</c> through their generic contracts
/// correctly map to <c>null</c> when invoked via <see cref="IHandler"/>.
/// </remarks>
public class IHandlerImplementationTests(ExecutionContextFixture executionContextFixture)
    : BaseHandlerFixture(executionContextFixture)
{
    
    
    /// <summary>
    /// Verifies that a handler implementing 
    /// <see cref="IHandler{TMessage, TResult}"/> with a <c>void</c>-like result 
    /// returns <c>null</c> when invoked via the non-generic <see cref="IHandler"/> interface.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]

    public void IHandlerShouldImplementIHandlerTMessageTResult()
    {
        // arrange
        var ctx = ExecutionContextFixture.PropagateAmbientContext().Ctx;
   
        IHandler handler = new StubVoidHandler();
        // act
        var result = handler.Handle(Message, ctx);
        // assert
        Assert.Null(result); 
    }
}