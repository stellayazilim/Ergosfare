using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Queries.Abstractions;

namespace Stella.Ergosfare.Queries.Test;

/// <summary>
/// Verifies that the default interface implementations of the typed query interceptor
/// interfaces forward the untyped pipeline calls to their type-safe members.
/// </summary>
/// <remarks>
/// The stub interceptors are pass-through and record invocation on instance flags: module
/// tests may register this whole assembly, so stubs must not alter pipeline results.
/// </remarks>
public class QueryInterceptorDefaultImplementationTests
{
    // These probes are invoked directly by the tests below, never dispatched — deliberately
    // outside the compiled closure, which is what silences ERGO001 for the private types.
    [ExcludeFromDiscovery]
    private record TestQuery : IQuery<string>;

    [ExcludeFromDiscovery]
    private class TestPreInterceptor : IQueryPreInterceptor<TestQuery, TestQuery>
    {
        public bool Called;

        public ValueTask<TestQuery?> HandleAsync(TestQuery query, ErgosfareContext executionContext)
        {
            Called = true;
            return ValueTask.FromResult<TestQuery?>(query);
        }
    }

    [ExcludeFromDiscovery]
    private class TestPostInterceptor : IQueryPostInterceptor<TestQuery, string>
    {
        public bool Called;

        public ValueTask<string> HandleAsync(TestQuery query, string result, ErgosfareContext executionContext)
        {
            Called = true;

            // The stage handled the failure, so it owes a result; the incoming slot is empty
            // here because the handler threw before producing one.
            return ValueTask.FromResult(result ?? string.Empty);
        }
    }

    [ExcludeFromDiscovery]
    private class TestExceptionInterceptor : IQueryExceptionInterceptor<TestQuery, string>
    {
        public bool Called;

        public ValueTask<string> HandleAsync(TestQuery query, string? result, Exception exception, ErgosfareContext context)
        {
            Called = true;

            // Handling means answering: the slot is empty because the handler threw before
            // it produced anything.
            return ValueTask.FromResult(result ?? string.Empty);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task PreInterceptorDefaultImplementation_ShouldForwardToTypedHandleAsync()
    {
        var interceptor = new TestPreInterceptor();
        var query = new TestQuery();

        var result = await ((IAsyncPreInterceptor<TestQuery>) interceptor).HandleAsync(query, Context);

        Assert.True(interceptor.Called);
        Assert.Same(query, result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task PostInterceptorDefaultImplementation_ShouldForwardToTypedHandleAsync()
    {
        var interceptor = new TestPostInterceptor();

        var result = await ((IAsyncPostInterceptor<TestQuery, string>) interceptor).HandleAsync(
            new TestQuery(), "result", Context);

        Assert.True(interceptor.Called);
        Assert.Equal("result", result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ExceptionInterceptorDefaultImplementation_ShouldForwardToTypedHandleAsync()
    {
        var interceptor = new TestExceptionInterceptor();

        var result = await ((IAsyncExceptionInterceptor<TestQuery, string>) interceptor).HandleAsync(
            new TestQuery(), "original", new Exception("boom"), Context);

        Assert.True(interceptor.Called);
        Assert.Equal("original", result);
    }

    /// <summary>
    /// A module-wide policy: every query, one exception flavour. Extending
    /// <see cref="IQueryExceptionInterceptorFor{TException}"/> makes the type an
    /// <see cref="IQuery"/> too, which is why it is kept out of discovery.
    /// </summary>
    [ExcludeFromDiscovery]
    private sealed class TimeoutPolicy : IQueryExceptionInterceptorFor<TimeoutException>
    {
        public TimeoutException? Seen;

        public ValueTask<object> HandleAsync(IQuery query, object? messageResult, TimeoutException exception,
            ErgosfareContext context)
        {
            Seen = exception;
            return ValueTask.FromResult(messageResult ?? "recovered");
        }
    }

    /// <summary>A flavour of the accepted exception, to prove the filter matches derived types.</summary>
    private sealed class RequestTimeoutException : TimeoutException;

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task FilteredModuleWideInterceptor_CastsTheExceptionToItsDeclaredFlavour()
    {
        var interceptor = new TimeoutPolicy();
        var thrown = new RequestTimeoutException();

        // The pipeline only ever calls the untyped member; the default body is what turns
        // the Exception it was handed into the flavour the policy was written against.
        var result = await ((IAsyncExceptionInterceptor<IQuery>) interceptor).HandleAsync(
            new TestQuery(), null, thrown, Context);

        Assert.Same(thrown, interceptor.Seen);
        Assert.Equal("recovered", result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void FilteredModuleWideInterceptor_MatchesWithCatchSemantics()
    {
        var filter = (IExceptionInterceptorFilter) new TimeoutPolicy();

        // The cast in the default body cannot fail because this is what runs first: the
        // stage asks the filter before it invokes the interceptor.
        Assert.True(filter.Matches(new TimeoutException()));
        Assert.True(filter.Matches(new RequestTimeoutException()));
        Assert.False(filter.Matches(new InvalidOperationException()));
    }

    /// <summary>
    /// A plain, unpooled <see cref="ErgosfareContext"/>; the default implementations under
    /// test never touch the context, so one shared instance is enough.
    /// </summary>
    private static readonly ErgosfareContext Context = new();
}
