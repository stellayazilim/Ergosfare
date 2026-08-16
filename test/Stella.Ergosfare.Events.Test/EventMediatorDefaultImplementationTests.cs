using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Events.Abstractions;

namespace Stella.Ergosfare.Events.Test;

/// <summary>
/// The convenience overloads on <see cref="IEventMediator"/> are default interface
/// implementations over the three full calls, so what they promise is a forwarding: which
/// lane each one lands in, and what the group filter has become by the time it gets there.
/// </summary>
/// <remarks>
/// <para>
/// These run against a mediator that implements the three full calls and <b>nothing else</b>,
/// which is the only way the default bodies execute at all — <c>EventMediator</c> overrides
/// them, so publishing through the shipped mediator proves nothing about the defaults. It is
/// also the shape a third-party implementation has on the day it is written.
/// </para>
/// <para>
/// Which lane a publish lands in is decided by the call site's static type, not the event's:
/// an <see cref="IEvent"/>-typed variable reaches the untyped lane, and a concrete-typed one
/// reaches the typed lane whose pipeline comes from a static-generic slot. That is an
/// overload-resolution fact the conveniences depend on, so it is asserted rather than assumed.
/// </para>
/// </remarks>
public class EventMediatorDefaultImplementationTests
{
    private static readonly GroupSet Auditing = GroupSet.Of("evt.dim.auditing");

    // Never published — the recorder below is the only receiver — so this is kept out of the
    // compiled closure, which is also what silences ERGO001 for a private marker type.
    [ExcludeFromDiscovery]
    private sealed record Ping : IEvent;

    /// <summary>Which of the three full calls a convenience forwarded to.</summary>
    private enum Lane
    {
        None,
        Untyped,
        Context,
        Typed,
    }

    /// <summary>
    /// Implements exactly the three members <see cref="IEventMediator"/> declares without a
    /// body, and records what each one was handed.
    /// </summary>
    private sealed class RecordingMediator : IEventMediator
    {
        public Lane Landed { get; private set; }

        public object? Event { get; private set; }

        /// <summary>The forwarded filter, snapshotted — <c>null</c> is a distinct answer.</summary>
        public string[]? Groups { get; private set; }

        public CancellationToken Token { get; private set; }

        /// <summary>The type argument the typed lane was reached with.</summary>
        public Type? TypeArgument { get; private set; }

        public ValueTask PublishAsync(IEvent @event, IEnumerable<string>? groups,
            CancellationToken cancellationToken)
        {
            Record(Lane.Untyped, @event, groups, cancellationToken, null);
            return ValueTask.CompletedTask;
        }

        public ValueTask PublishAsync(IEvent @event, ErgosfareContext context, IEnumerable<string>? groups = null)
        {
            Record(Lane.Context, @event, groups, context.CancellationToken, null);
            return ValueTask.CompletedTask;
        }

        public ValueTask PublishAsync<TEvent>(TEvent @event, IEnumerable<string>? groups,
            CancellationToken cancellationToken) where TEvent : notnull
        {
            Record(Lane.Typed, @event, groups, cancellationToken, typeof(TEvent));
            return ValueTask.CompletedTask;
        }

        private void Record(Lane lane, object @event, IEnumerable<string>? groups, CancellationToken token,
            Type? typeArgument)
        {
            Landed = lane;
            Event = @event;
            Groups = groups?.ToArray();
            Token = token;
            TypeArgument = typeArgument;
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task UntypedPublish_ReachesTheUntypedLaneWithNoFilter()
    {
        var recorder = new RecordingMediator();
        IEventMediator mediator = recorder;
        using var cts = new CancellationTokenSource();

        // Statically an IEvent: the non-generic overload wins the tie, so this is the lane
        // a publish of a message read off a queue takes.
        IEvent erased = new Ping();

        await mediator.PublishAsync(erased, cts.Token);

        Assert.Equal(Lane.Untyped, recorder.Landed);
        Assert.Same(erased, recorder.Event);
        Assert.Null(recorder.Groups);
        Assert.Equal(cts.Token, recorder.Token);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task UntypedPublish_WithAGroupSet_ForwardsTheNames_AndTheEmptySetAsNoFilter()
    {
        var recorder = new RecordingMediator();
        IEventMediator mediator = recorder;
        IEvent erased = new Ping();

        await mediator.PublishAsync(erased, Auditing);

        Assert.Equal(Lane.Untyped, recorder.Landed);
        Assert.Equal(new[] { "evt.dim.auditing" }, recorder.Groups);

        // The empty set is not a filter naming nothing — it is the absence of a filter.
        await mediator.PublishAsync(erased, GroupSet.Empty);

        Assert.Equal(Lane.Untyped, recorder.Landed);
        Assert.Null(recorder.Groups);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task TypedPublish_CarriesTheConcreteTypeIntoTheTypedLane()
    {
        var recorder = new RecordingMediator();
        IEventMediator mediator = recorder;
        using var cts = new CancellationTokenSource();
        var ping = new Ping();

        await mediator.PublishAsync(ping, cts.Token);

        // The whole point of the typed lane is that the pipeline is found by the type
        // argument, so the argument reaching it must be the event's own type.
        Assert.Equal(Lane.Typed, recorder.Landed);
        Assert.Equal(typeof(Ping), recorder.TypeArgument);
        Assert.Same(ping, recorder.Event);
        Assert.Null(recorder.Groups);
        Assert.Equal(cts.Token, recorder.Token);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task TypedPublish_WithAGroupSet_FiltersWithoutLeavingTheTypedLane()
    {
        var recorder = new RecordingMediator();
        IEventMediator mediator = recorder;
        var ping = new Ping();

        await mediator.PublishAsync(ping, Auditing);

        Assert.Equal(Lane.Typed, recorder.Landed);
        Assert.Equal(typeof(Ping), recorder.TypeArgument);
        Assert.Equal(new[] { "evt.dim.auditing" }, recorder.Groups);

        await mediator.PublishAsync(ping, GroupSet.Empty);

        // Filtering by nothing must not cost the type argument: an unfiltered typed publish
        // is still a typed publish.
        Assert.Equal(Lane.Typed, recorder.Landed);
        Assert.Equal(typeof(Ping), recorder.TypeArgument);
        Assert.Null(recorder.Groups);
    }
}
