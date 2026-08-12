using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Contract.Test.Harness;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Events.Abstractions;
using Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Generated;

namespace Stella.Ergosfare.Contract.Test.Events;

/// <summary>
/// The synchronous main-handler contracts inside an event broadcast — the fan-out twin of
/// <c>Sync/SyncMainHandlerTests.cs</c>. A publish resolves every handler of the event, and
/// each fan-out slot dispatches through its own contract: the two synchronous shapes pinned
/// here run beside an asynchronous sibling in one broadcast. Each shape is exercised twice —
/// once on a bare publish, and once behind an interceptor, which is a different fan-out
/// implementation the same way it is for commands.
/// </summary>
/// <remarks>
/// These types stay keyed: a publish never enters a compile-time plan — the generator emits
/// plans for sole-handler command and query dispatches only — so both axes fan out
/// reflectively, and the fallback axis pins the reflective descriptor construction the same
/// way the command-side area does.
/// <para>
/// The synchronous bases implement <see cref="IEvent"/> themselves for the same reason the
/// command-side ones implement <c>ICommand</c>: the module builders reject any type without
/// their marker, and the bare synchronous contracts have no event-flavored facade to
/// inherit it from — see the suite README, suspicious behavior 9.
/// </para>
/// </remarks>
public abstract class SyncEventHandlerContract
{
    /// <summary>The discovery key this area registers under.</summary>
    protected const string Key = "contract.broadcast";

    /// <summary>Marks itself and reports which contract served its slot of the broadcast.</summary>
    [ExcludeFromDiscovery]
    public abstract class TaskShapedHandlerBase<TEvent> : IEvent, IHandler<TEvent, ValueTask>
        where TEvent : class, IEvent
    {
        /// <inheritdoc />
        public ValueTask Handle(TEvent message, ErgosfareContext context)
        {
            context.Mark("sync:valuetask");
            return ValueTask.CompletedTask;
        }
    }

    /// <inheritdoc cref="TaskShapedHandlerBase{TEvent}"/>
    [ExcludeFromDiscovery]
    public abstract class BareShapedHandlerBase<TEvent> : IEvent, IHandler<TEvent, object>
        where TEvent : class, IEvent
    {
        /// <inheritdoc />
        public object Handle(TEvent message, ErgosfareContext context)
        {
            context.Mark("sync:object");
            return "ignored";
        }
    }

    /// <summary>The asynchronous sibling the synchronous shapes fan out beside.</summary>
    [ExcludeFromDiscovery]
    public abstract class AsyncSiblingHandlerBase<TEvent> : IEventHandler<TEvent>
        where TEvent : class, IEvent
    {
        /// <inheritdoc />
        public ValueTask HandleAsync(TEvent @event, ErgosfareContext context)
        {
            context.Mark("async");
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>Forces the interceptor-carrying fan-out, the way the command-side gate does.</summary>
    [ExcludeFromDiscovery]
    public abstract class GatePreBase<TEvent> : IEventPreInterceptor<TEvent>
        where TEvent : class, IEvent
    {
        /// <inheritdoc />
        public ValueTask<TEvent> HandleAsync(TEvent @event, ErgosfareContext context)
        {
            context.Mark("pre");
            return ValueTask.FromResult(@event);
        }
    }

    /// <summary>A container with this axis' broadcast handlers registered its own way.</summary>
    protected abstract ServiceProvider CreateProvider();

    /// <summary>The event all three handler shapes claim, no interceptors.</summary>
    protected abstract IEvent NewEvent();

    /// <summary>Its sibling whose fan-out sits behind an interceptor.</summary>
    protected abstract IEvent NewInterceptedEvent();

    private PipelineRecorder NewRecorder() => new() { Label = GetType().Name };

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_publish_fans_out_to_the_synchronous_shapes_beside_their_asynchronous_sibling()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();

        await provider.GetRequiredService<IEventMediator>()
            .PublishAsync(NewEvent(), recorder.Events());

        // Observed fan-out order, identical on both axes: it follows neither the keyed
        // declaration order nor the runtime registration order (both say async, task,
        // bare) — pinned as observed.
        recorder.AssertStages("async", "sync:object", "sync:valuetask");
    }

    [Fact]
    [Trait("Category", "Contract")]
    public async Task A_publish_that_carries_an_interceptor_still_reaches_the_synchronous_shapes()
    {
        await using var provider = CreateProvider();
        var recorder = NewRecorder();

        await provider.GetRequiredService<IEventMediator>()
            .PublishAsync(NewInterceptedEvent(), recorder.Events());

        recorder.AssertStages("pre", "async", "sync:object", "sync:valuetask");
    }
}

/// <summary>The broadcast handler types registered through generated descriptors.</summary>
public static class KeyedBroadcastTypes
{
    /// <summary>The event all three handler shapes claim, no interceptors.</summary>
    [DiscoveryKey("contract.broadcast")]
    public sealed class MixedShapesEvent : IEvent;

    /// <inheritdoc />
    [DiscoveryKey("contract.broadcast")]
    public sealed class MixedShapesAsyncHandler : SyncEventHandlerContract.AsyncSiblingHandlerBase<MixedShapesEvent>;

    /// <inheritdoc />
    [DiscoveryKey("contract.broadcast")]
    public sealed class MixedShapesTaskHandler : SyncEventHandlerContract.TaskShapedHandlerBase<MixedShapesEvent>;

    /// <inheritdoc />
    [DiscoveryKey("contract.broadcast")]
    public sealed class MixedShapesBareHandler : SyncEventHandlerContract.BareShapedHandlerBase<MixedShapesEvent>;

    /// <summary>The event whose fan-out sits behind an interceptor.</summary>
    [DiscoveryKey("contract.broadcast")]
    public sealed class InterceptedMixedEvent : IEvent;

    /// <inheritdoc />
    [DiscoveryKey("contract.broadcast")]
    public sealed class InterceptedMixedAsyncHandler : SyncEventHandlerContract.AsyncSiblingHandlerBase<InterceptedMixedEvent>;

    /// <inheritdoc />
    [DiscoveryKey("contract.broadcast")]
    public sealed class InterceptedMixedTaskHandler : SyncEventHandlerContract.TaskShapedHandlerBase<InterceptedMixedEvent>;

    /// <inheritdoc />
    [DiscoveryKey("contract.broadcast")]
    public sealed class InterceptedMixedBareHandler : SyncEventHandlerContract.BareShapedHandlerBase<InterceptedMixedEvent>;

    /// <inheritdoc />
    [DiscoveryKey("contract.broadcast")]
    public sealed class InterceptedMixedPre : SyncEventHandlerContract.GatePreBase<InterceptedMixedEvent>;
}

/// <summary>The same shapes, hidden from the generator so <c>Register&lt;T&gt;()</c> is reflective.</summary>
public static class FallbackBroadcastTypes
{
    /// <inheritdoc cref="KeyedBroadcastTypes.MixedShapesEvent"/>
    [ExcludeFromDiscovery]
    public sealed class MixedShapesEvent : IEvent;

    /// <inheritdoc />
    [ExcludeFromDiscovery]
    public sealed class MixedShapesAsyncHandler : SyncEventHandlerContract.AsyncSiblingHandlerBase<MixedShapesEvent>;

    /// <inheritdoc />
    [ExcludeFromDiscovery]
    public sealed class MixedShapesTaskHandler : SyncEventHandlerContract.TaskShapedHandlerBase<MixedShapesEvent>;

    /// <inheritdoc />
    [ExcludeFromDiscovery]
    public sealed class MixedShapesBareHandler : SyncEventHandlerContract.BareShapedHandlerBase<MixedShapesEvent>;

    /// <inheritdoc cref="KeyedBroadcastTypes.InterceptedMixedEvent"/>
    [ExcludeFromDiscovery]
    public sealed class InterceptedMixedEvent : IEvent;

    /// <inheritdoc />
    [ExcludeFromDiscovery]
    public sealed class InterceptedMixedAsyncHandler : SyncEventHandlerContract.AsyncSiblingHandlerBase<InterceptedMixedEvent>;

    /// <inheritdoc />
    [ExcludeFromDiscovery]
    public sealed class InterceptedMixedTaskHandler : SyncEventHandlerContract.TaskShapedHandlerBase<InterceptedMixedEvent>;

    /// <inheritdoc />
    [ExcludeFromDiscovery]
    public sealed class InterceptedMixedBareHandler : SyncEventHandlerContract.BareShapedHandlerBase<InterceptedMixedEvent>;

    /// <inheritdoc />
    [ExcludeFromDiscovery]
    public sealed class InterceptedMixedPre : SyncEventHandlerContract.GatePreBase<InterceptedMixedEvent>;
}

/// <summary>The broadcast contract under generated (keyed) registration.</summary>
public sealed class GeneratedRegistrationSyncEventHandlerTests : SyncEventHandlerContract
{
    /// <inheritdoc />
    protected override ServiceProvider CreateProvider()
        => new ServiceCollection()
            .AddErgosfare(options => options
                .AddEventModule(events => events.RegisterGenerated(Key)))
            .BuildServiceProvider();

    /// <inheritdoc />
    protected override IEvent NewEvent() => new KeyedBroadcastTypes.MixedShapesEvent();

    /// <inheritdoc />
    protected override IEvent NewInterceptedEvent() => new KeyedBroadcastTypes.InterceptedMixedEvent();
}

/// <summary>The same contract under explicit runtime registration.</summary>
public sealed class RuntimeRegistrationSyncEventHandlerTests : SyncEventHandlerContract
{
    /// <inheritdoc />
    protected override ServiceProvider CreateProvider()
        => new ServiceCollection()
            .AddErgosfare(options => options
                .AddEventModule(events => events
                    .Register<FallbackBroadcastTypes.MixedShapesAsyncHandler>()
                    .Register<FallbackBroadcastTypes.MixedShapesTaskHandler>()
                    .Register<FallbackBroadcastTypes.MixedShapesBareHandler>()
                    .Register<FallbackBroadcastTypes.InterceptedMixedAsyncHandler>()
                    .Register<FallbackBroadcastTypes.InterceptedMixedTaskHandler>()
                    .Register<FallbackBroadcastTypes.InterceptedMixedBareHandler>()
                    .Register<FallbackBroadcastTypes.InterceptedMixedPre>()))
            .BuildServiceProvider();

    /// <inheritdoc />
    protected override IEvent NewEvent() => new FallbackBroadcastTypes.MixedShapesEvent();

    /// <inheritdoc />
    protected override IEvent NewInterceptedEvent() => new FallbackBroadcastTypes.InterceptedMixedEvent();
}
