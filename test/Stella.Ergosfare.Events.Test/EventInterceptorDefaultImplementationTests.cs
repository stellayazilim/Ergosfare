using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Abstractions.Handlers;
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
    // These probes are invoked directly by the tests below, never dispatched — deliberately
    // outside the compiled closure, which is what silences ERGO001 for the private types.
    [ExcludeFromDiscovery]
    private record TestEvent : IEvent;

    [ExcludeFromDiscovery]
    private class TestPreInterceptor : IEventPreInterceptor
    {
        public bool Called;

        public ValueTask HandleAsync(IEvent @event, ErgosfareContext executionContext)
        {
            Called = true;
            return ValueTask.CompletedTask;
        }
    }

    private class TestTypedPreInterceptor : IEventPreInterceptor<TestEvent, TestEvent>
    {
        public static readonly TestEvent Replacement = new();

        public ValueTask<TestEvent> HandleAsync(TestEvent @event, ErgosfareContext context)
        {
            return ValueTask.FromResult(Replacement);
        }
    }

    [ExcludeFromDiscovery]
    private class TestPostInterceptor : IEventPostInterceptor
    {
        public bool Called;

        public ValueTask HandleAsync(IEvent @event, ValueTask result, ErgosfareContext executionContext)
        {
            Called = true;
            return ValueTask.CompletedTask;
        }
    }

    [ExcludeFromDiscovery]
    private class TestTypedPostInterceptor : IEventPostInterceptor<TestEvent>
    {
        public bool Called;

        public ValueTask HandleAsync(TestEvent @event, ValueTask result, ErgosfareContext executionContext)
        {
            Called = true;
            return ValueTask.CompletedTask;
        }
    }

    // ReSharper disable once RedundantArgumentDefaultValue
    // ReSharper disable once PreferConcreteValueOverDefault
    private static ErgosfareContext CreateContext() => new();

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task PreInterceptorDefaultImplementation_ShouldForwardToTypedHandleAsync()
    {
        var interceptor = new TestPreInterceptor();
        var @event = new TestEvent();

        var result = await ((IAsyncPreInterceptor<IEvent>) interceptor).HandleAsync(@event, CreateContext());

        Assert.True(interceptor.Called);

        // Regression: the default implementation used to return ValueTask.CompletedTask,
        // which the pipeline then cast to the message type and crashed — a pre-interceptor's
        // return value is the message the rest of the pipeline continues with.
        Assert.Same(@event, result);
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
        await ((IAsyncPostInterceptor<IEvent>) interceptor).HandleAsync(new TestEvent(), ValueTask.CompletedTask, CreateContext());

        Assert.True(interceptor.Called);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task TypedPostInterceptorDefaultImplementation_ShouldForwardToTypedHandleAsync()
    {
        var interceptor = new TestTypedPostInterceptor();

        // Regression: this call used to recurse infinitely instead of reaching the typed member.
        await ((IAsyncPostInterceptor<TestEvent>) interceptor).HandleAsync(new TestEvent(), ValueTask.CompletedTask, CreateContext());

        Assert.True(interceptor.Called);
    }

    [ExcludeFromDiscovery]
    private sealed class TestExceptionInterceptor : IEventExceptionInterceptor
    {
        public Exception? Seen;

        public ValueTask HandleAsync(IEvent @event, Exception exception, ErgosfareContext context)
        {
            Seen = exception;
            return ValueTask.CompletedTask;
        }
    }

    [ExcludeFromDiscovery]
    private sealed class TestFinalInterceptor : IEventFinalInterceptor
    {
        public bool Called;

        public Exception? Seen;

        public ValueTask HandleAsync(IEvent @event, Exception? exception, ErgosfareContext context)
        {
            Called = true;
            Seen = exception;
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ExceptionInterceptorDefaultImplementation_AnswersTheResultlessSlotItself()
    {
        var interceptor = new TestExceptionInterceptor();
        var thrown = new InvalidOperationException("boom");

        var result = await ((IAsyncExceptionInterceptor<IEvent, Unit>) interceptor).HandleAsync(
            new TestEvent(), null, thrown, CreateContext());

        Assert.Same(thrown, interceptor.Seen);

        // The stage threads a result slot a publish has no way to fill, so the default body
        // fills it — an implementor is never handed a parameter with one legal value.
        Assert.Equal(Unit.Value, result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task FinalInterceptorDefaultImplementation_ForwardsTheFailureAndDropsTheResult()
    {
        var interceptor = new TestFinalInterceptor();
        var thrown = new InvalidOperationException("boom");

        await ((IAsyncFinalInterceptor<IEvent, Unit>) interceptor).HandleAsync(
            new TestEvent(), null, thrown, CreateContext());

        Assert.True(interceptor.Called);
        Assert.Same(thrown, interceptor.Seen);

        // A publish that settled without failing arrives with no exception, and that is the
        // only difference the final stage sees between the two paths.
        var settled = new TestFinalInterceptor();

        await ((IAsyncFinalInterceptor<IEvent, Unit>) settled).HandleAsync(
            new TestEvent(), null, null, CreateContext());

        Assert.True(settled.Called);
        Assert.Null(settled.Seen);
    }
}
