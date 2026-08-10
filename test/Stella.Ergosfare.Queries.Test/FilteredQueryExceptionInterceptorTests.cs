using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Queries.Abstractions;

namespace Stella.Ergosfare.Queries.Test;

/// <summary>
/// Verifies the filtered query exception-interceptor facades: their filter probe answers
/// with <c>catch</c> semantics, and their default interface implementations forward the
/// pipeline's untyped call to the typed member with the exception already narrowed.
/// </summary>
public class FilteredQueryExceptionInterceptorTests
{
    private sealed record TestQuery : IQuery<string>;

    private class TestFault() : Exception("fault");

    private sealed class DerivedTestFault : TestFault;

    private sealed class ResultTypedQueryInterceptor : IQueryExceptionInterceptorFor<TestQuery, string, TestFault>
    {
        public const string Recovery = "recovered";

        public TestFault? Received;

        public ValueTask<string?> HandleAsync(
            TestQuery query, string? result, TestFault exception, IExecutionContext context)
        {
            Received = exception;
            return ValueTask.FromResult<string?>(Recovery);
        }
    }

    private sealed class ResultAgnosticQueryInterceptor : IQueryExceptionInterceptorFor<TestQuery, TestFault>
    {
        public TestFault? Received;

        public ValueTask<object> HandleAsync(
            TestQuery query, object? messageResult, TestFault exception, IExecutionContext context)
        {
            Received = exception;
            return ValueTask.FromResult<object>(messageResult ?? Unit.Value);
        }
    }

    // These facades hand the context straight through to the typed member without reading
    // it, and the concrete context is internal to the core assembly.
    private static IExecutionContext CreateContext() => null!;

    [Fact]
    [Trait("Category", "Unit")]
    public void FilterMatchesTheDeclaredExceptionTypeAndItsSubtypes()
    {
        IExceptionInterceptorFilter filter = new ResultTypedQueryInterceptor();

        Assert.True(filter.Matches(new TestFault()));

        // catch semantics: a subtype of the declared fault matches too.
        Assert.True(filter.Matches(new DerivedTestFault()));
        Assert.False(filter.Matches(new InvalidOperationException()));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ResultTypedFacadeDefaultImplementationForwardsTheNarrowedException()
    {
        var interceptor = new ResultTypedQueryInterceptor();
        var fault = new DerivedTestFault();

        var result = await ((IAsyncExceptionInterceptor<TestQuery, string>) interceptor)
            .HandleAsync(new TestQuery(), "original", fault, CreateContext());

        Assert.Same(fault, interceptor.Received);
        Assert.Equal(ResultTypedQueryInterceptor.Recovery, result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ResultAgnosticFacadeDefaultImplementationForwardsTheNarrowedException()
    {
        var interceptor = new ResultAgnosticQueryInterceptor();
        var fault = new TestFault();

        var result = await ((IAsyncExceptionInterceptor<TestQuery>) interceptor)
            .HandleAsync(new TestQuery(), "original", fault, CreateContext());

        Assert.Same(fault, interceptor.Received);
        Assert.Equal("original", result);
    }
}
