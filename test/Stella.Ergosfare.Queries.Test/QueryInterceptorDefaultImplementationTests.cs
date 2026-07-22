using Stella.Ergosfare.Core.Abstractions;
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
    private record TestQuery : IQuery<string>;

    private class TestPreInterceptor : IQueryPreInterceptor<TestQuery, TestQuery>
    {
        public bool Called;

        public ValueTask<TestQuery?> HandleAsync(TestQuery query, IExecutionContext executionContext)
        {
            Called = true;
            return ValueTask.FromResult<TestQuery?>(query);
        }
    }

    private class TestPostInterceptor : IQueryPostInterceptor<TestQuery, string>
    {
        public bool Called;

        public ValueTask<string> HandleAsync(TestQuery query, string result, IExecutionContext executionContext)
        {
            Called = true;
            return ValueTask.FromResult(result);
        }
    }

    private class TestExceptionInterceptor : IQueryExceptionInterceptor<TestQuery, string>
    {
        public bool Called;

        public ValueTask<string?> HandleAsync(TestQuery query, string? result, Exception exception, IExecutionContext context)
        {
            Called = true;
            return ValueTask.FromResult(result);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task PreInterceptorDefaultImplementation_ShouldForwardToTypedHandleAsync()
    {
        var interceptor = new TestPreInterceptor();
        var query = new TestQuery();

        var result = await ((IAsyncPreInterceptor<TestQuery>) interceptor).HandleAsync(query, FakeExecutionContext.Instance);

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
            new TestQuery(), "result", FakeExecutionContext.Instance);

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
            new TestQuery(), "original", new Exception("boom"), FakeExecutionContext.Instance);

        Assert.True(interceptor.Called);
        Assert.Equal("original", result);
    }

    /// <summary>
    /// Minimal <see cref="IExecutionContext"/> stand-in; the default implementations under
    /// test never touch the context.
    /// </summary>
    private sealed class FakeExecutionContext : IExecutionContext
    {
        public static readonly FakeExecutionContext Instance = new();

        public CancellationToken CancellationToken => CancellationToken.None;
        public IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public void Set(string key, object item) => Items[key] = item;
        public bool Has(string key) => Items.ContainsKey(key);
        public TType Get<TType>(string key) where TType : notnull => (TType) Items[key]!;
        public bool TryGet<TType>(string key, out TType item)
        {
            if (Items.TryGetValue(key, out var value))
            {
                item = (TType) value!;
                return true;
            }

            item = default!;
            return false;
        }
        public void Abort(object? messageResult = null) => throw new NotSupportedException();
    }
}
