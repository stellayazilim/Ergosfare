using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Events.Abstractions;

namespace Stella.Ergosfare.Events.Test;

/// <summary>
/// Verifies the filtered event exception-interceptor facades: their filter probe answers
/// with <c>catch</c> semantics, and their default interface implementations forward the
/// pipeline's untyped call to the typed member with the exception already narrowed.
/// </summary>
/// <remarks>
/// A publish produces no result, so these facades return the pipeline's one result value
/// rather than anything of the interceptor's own — the assertion below is what keeps the
/// broadcast stage's result slot well-formed.
/// </remarks>
public class FilteredEventExceptionInterceptorTests
{
    // These probes are invoked directly by the tests below, never dispatched — deliberately
    // outside the compiled closure, which is what silences ERGO001 for the private types.
    [ExcludeFromDiscovery]
    private sealed record TestEvent : IEvent;

    private sealed class TestFault() : Exception("fault");

    private sealed class DerivedTestFault() : Exception("derived");

    [ExcludeFromDiscovery]
    private sealed class TypedEventInterceptor : IEventExceptionInterceptorFor<TestEvent, TestFault>
    {
        public TestFault? Received;

        public ValueTask HandleAsync(TestEvent @event, TestFault exception, ErgosfareContext context)
        {
            Received = exception;
            return ValueTask.CompletedTask;
        }
    }

    [ExcludeFromDiscovery]
    private sealed class ModuleWideEventInterceptor : IEventExceptionInterceptorFor<TestFault>
    {
        public TestFault? Received;

        public ValueTask HandleAsync(IEvent @event, TestFault exception, ErgosfareContext context)
        {
            Received = exception;
            return ValueTask.CompletedTask;
        }
    }

    private static ErgosfareContext CreateContext() => new(null, default);

    [Fact]
    [Trait("Category", "Unit")]
    public void FilterAcceptsTheDeclaredExceptionTypeAndRejectsOthers()
    {
        IExceptionInterceptorFilter filter = new TypedEventInterceptor();

        Assert.True(filter.Matches(new TestFault()));
        Assert.False(filter.Matches(new DerivedTestFault()));
        Assert.False(filter.Matches(new InvalidOperationException()));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task TypedFacadeDefaultImplementationForwardsTheNarrowedException()
    {
        var interceptor = new TypedEventInterceptor();
        var fault = new TestFault();

        var result = await ((IAsyncExceptionInterceptor<TestEvent, Unit>) interceptor)
            .HandleAsync(new TestEvent(), Unit.Value, fault, CreateContext());

        Assert.Same(fault, interceptor.Received);

        // The publish has no result of its own; the slot keeps the pipeline's one value.
        Assert.Same(Unit.Value, result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ModuleWideFacadeDefaultImplementationForwardsTheNarrowedException()
    {
        var interceptor = new ModuleWideEventInterceptor();
        var fault = new TestFault();

        var result = await ((IAsyncExceptionInterceptor<IEvent, Unit>) interceptor)
            .HandleAsync(new TestEvent(), Unit.Value, fault, CreateContext());

        Assert.Same(fault, interceptor.Received);
        Assert.Same(Unit.Value, result);
    }
}
