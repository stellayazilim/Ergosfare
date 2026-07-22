using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Internal.Contexts;
using Stella.Ergosfare.Events.Abstractions;

namespace Stella.Ergosfare.Events.Test;

/// <summary>
/// Verifies that the default interface implementations of the typed event interceptor
/// interfaces forward the untyped pipeline calls to their type-safe members. The
/// post-interceptor cases are regression tests: their default implementations used to
/// re-bind to themselves and recurse infinitely instead of calling the typed member.
/// </summary>
public class EventInterceptorDefaultImplementationTests
{
    private record TestEvent : IEvent;

    private class TestPreInterceptor : IEventPreInterceptor
    {
        public bool Called;

        public Task HandleAsync(IEvent @event, IExecutionContext executionContext)
        {
            Called = true;
            return Task.CompletedTask;
        }
    }

    private class TestTypedPreInterceptor : IEventPreInterceptor<TestEvent, TestEvent>
    {
        public static readonly TestEvent Replacement = new();

        public Task<TestEvent> HandleAsync(TestEvent @event, IExecutionContext context)
        {
            return Task.FromResult(Replacement);
        }
    }

    private class TestPostInterceptor : IEventPostInterceptor
    {
        public bool Called;

        public Task HandleAsync(IEvent @event, Task result, IExecutionContext executionContext)
        {
            Called = true;
            return Task.CompletedTask;
        }
    }

    private class TestTypedPostInterceptor : IEventPostInterceptor<TestEvent>
    {
        public bool Called;

        public Task HandleAsync(TestEvent @event, Task result, IExecutionContext executionContext)
        {
            Called = true;
            return Task.CompletedTask;
        }
    }

    private static ErgosfareExecutionContext CreateContext() => new(null, default);

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task PreInterceptorDefaultImplementation_ShouldForwardToTypedHandleAsync()
    {
        var interceptor = new TestPreInterceptor();

        await ((IAsyncPreInterceptor<IEvent>) interceptor).HandleAsync(new TestEvent(), CreateContext());

        Assert.True(interceptor.Called);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task TypedPreInterceptorDefaultImplementation_ShouldReturnModifiedEvent()
    {
        IAsyncPreInterceptor<TestEvent> interceptor = new TestTypedPreInterceptor();

        var result = await interceptor.HandleAsync(new TestEvent(), CreateContext());

        Assert.Same(TestTypedPreInterceptor.Replacement, result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task PostInterceptorDefaultImplementation_ShouldForwardToTypedHandleAsync()
    {
        var interceptor = new TestPostInterceptor();

        // Regression: this call used to recurse infinitely instead of reaching the typed member.
        await ((IAsyncPostInterceptor<IEvent>) interceptor).HandleAsync(new TestEvent(), Task.CompletedTask, CreateContext());

        Assert.True(interceptor.Called);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task TypedPostInterceptorDefaultImplementation_ShouldForwardToTypedHandleAsync()
    {
        var interceptor = new TestTypedPostInterceptor();

        // Regression: this call used to recurse infinitely instead of reaching the typed member.
        await ((IAsyncPostInterceptor<TestEvent>) interceptor).HandleAsync(new TestEvent(), Task.CompletedTask, CreateContext());

        Assert.True(interceptor.Called);
    }
}
