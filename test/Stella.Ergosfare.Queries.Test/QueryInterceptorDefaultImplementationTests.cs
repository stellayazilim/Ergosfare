using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Queries.Abstractions;

namespace Stella.Ergosfare.Queries.Test;

/// <summary>
/// Verifies that the default interface implementations of the typed query interceptor
/// interfaces forward the untyped pipeline calls to their type-safe members.
/// </summary>
public class QueryInterceptorDefaultImplementationTests
{
    private record TestQuery : IQuery<string>;

    private class TestPreInterceptor : IQueryPreInterceptor<TestQuery, TestQuery>
    {
        public static readonly TestQuery Replacement = new();

        public Task<TestQuery?> HandleAsync(TestQuery query, IExecutionContext executionContext)
        {
            return Task.FromResult<TestQuery?>(Replacement);
        }
    }

    private class TestPostInterceptor : IQueryPostInterceptor<TestQuery, string, string>
    {
        public Task<string> HandleAsync(TestQuery query, string result, IExecutionContext executionContext)
        {
            return Task.FromResult(result + "-post");
        }
    }

    private class TestExceptionInterceptor : IQueryExceptionInterceptor<TestQuery, string, string>
    {
        public Task<string?> HandleAsync(TestQuery query, string? result, Exception exception, IExecutionContext context)
        {
            return Task.FromResult<string?>("recovered");
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task PreInterceptorDefaultImplementation_ShouldForwardToTypedHandleAsync()
    {
        IAsyncPreInterceptor<TestQuery> interceptor = new TestPreInterceptor();

        var result = await interceptor.HandleAsync(new TestQuery(), FakeExecutionContext.Instance);

        Assert.Same(TestPreInterceptor.Replacement, result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task PostInterceptorDefaultImplementation_ShouldForwardToTypedHandleAsync()
    {
        IAsyncPostInterceptor<TestQuery, string> interceptor = new TestPostInterceptor();

        var result = await interceptor.HandleAsync(new TestQuery(), "result", FakeExecutionContext.Instance);

        Assert.Equal("result-post", result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ExceptionInterceptorDefaultImplementation_ShouldForwardToTypedHandleAsync()
    {
        IAsyncExceptionInterceptor<TestQuery, string> interceptor = new TestExceptionInterceptor();

        var result = await interceptor.HandleAsync(
            new TestQuery(), "original", new Exception("boom"), FakeExecutionContext.Instance);

        Assert.Equal("recovered", result);
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
