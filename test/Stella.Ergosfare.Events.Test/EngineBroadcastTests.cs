using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Core;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Abstractions.Factories;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Events.Abstractions;
using Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection;

namespace Stella.Ergosfare.Events.Test;

/// <summary>
/// The engine's own broadcast surface — the three entry points the event facade is a typed
/// layer over. Each finds this container's pipeline for the event a different way (runtime
/// type, static-generic slot, or the slot's runtime-type guard rejecting a base-typed call),
/// and two of them own the execution context while the third does not.
/// </summary>
/// <remarks>
/// The suspended case is the one worth having: a publish that completes synchronously returns
/// its pooled context inline, while a handler that genuinely awaits sends the context through
/// a <c>finally</c> on whatever thread the delivery resumes on. Only the second path can
/// return a context twice or not at all, and it was reached by no test.
/// </remarks>
public class EngineBroadcastTests
{
    public sealed record BaseNotice : IEvent;

    public sealed record SlowNotice : IEvent;

    private static class Delivered
    {
        public static int Count;

        public static ErgosfareContext? Context;

        public static void Reset()
        {
            Count = 0;
            Context = null;
        }
    }

    [ExcludeFromDiscovery]
    public sealed class BaseNoticeHandler : IEventHandler<BaseNotice>
    {
        public ValueTask HandleAsync(BaseNotice @event, ErgosfareContext context)
        {
            Delivered.Count++;
            Delivered.Context = context;
            context.Set("engine.broadcast", true);
            return ValueTask.CompletedTask;
        }
    }

    [ExcludeFromDiscovery]
    public sealed class SlowNoticeHandler : IEventHandler<SlowNotice>
    {
        public async ValueTask HandleAsync(SlowNotice @event, ErgosfareContext context)
        {
            Delivered.Context = context;
            context.Set("engine.broadcast", true);

            // Never completes synchronously, so the publish above this frame cannot take
            // its inline-return shortcut.
            await Task.Yield();

            Delivered.Count++;
        }
    }

    private static ServiceProvider Build()
        => new ServiceCollection()
            .AddErgosfare(x => x.AddEventModule(e =>
            {
                e.Register<BaseNoticeHandler>();
                e.Register<SlowNoticeHandler>();
            }))
            .BuildServiceProvider();

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task UntypedBroadcast_ResolvesThePipelineByTheRuntimeType()
    {
        await using var provider = Build();
        var engine = provider.GetRequiredService<MessageDispatchEngine>();
        Delivered.Reset();

        // Statically an object: nothing but the runtime type can find the pipeline, which
        // is the shape a message read off a transport arrives in.
        object erased = new BaseNotice();

        await engine.BroadcastAsync(erased, provider);

        Assert.Equal(1, Delivered.Count);

        // The engine rented this one, so it is back in the pool with its state gone.
        Assert.False(Delivered.Context!.Has("engine.broadcast"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task TypedBroadcast_TakesTheSlot_AndFallsBackWhenTheTypeArgumentIsABase()
    {
        await using var provider = Build();
        var engine = provider.GetRequiredService<MessageDispatchEngine>();
        Delivered.Reset();

        // Concrete type argument: the static-generic slot answers.
        await engine.BroadcastAsync(new BaseNotice(), provider);

        // Base-typed argument: the runtime-type guard rejects the slot and the lookup
        // resolves the same pipeline, so the delivery is identical.
        IEvent erased = new BaseNotice();

        await engine.BroadcastAsync(erased, provider);

        Assert.Equal(2, Delivered.Count);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ContextBroadcast_LeavesTheCallersContextAlone()
    {
        await using var provider = Build();
        var engine = provider.GetRequiredService<MessageDispatchEngine>();
        var context = new ErgosfareContext();
        Delivered.Reset();

        await engine.BroadcastAsync(new BaseNotice(), context, provider);

        Assert.Equal(1, Delivered.Count);
        Assert.Same(context, Delivered.Context);

        // The nested-publish path rents nothing, so it returns nothing — what the handler
        // wrote is still there for the caller that owns the context.
        Assert.Equal(true, context.Items["engine.broadcast"]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task SuspendedBroadcast_ReturnsThePooledContext_AfterTheDeliveryResumes()
    {
        await using var provider = Build();
        var engine = provider.GetRequiredService<MessageDispatchEngine>();
        Delivered.Reset();

        await engine.BroadcastAsync(new SlowNotice(), provider);

        Assert.Equal(1, Delivered.Count);

        // Reclaimed from the awaiting helper's finally, not inline — the assertion is the
        // same either way, which is the point: the caller cannot tell, and must not.
        Assert.False(Delivered.Context!.Has("engine.broadcast"));
        Assert.False(Delivered.Context.CancellationToken.CanBeCanceled);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task EveryBroadcastEntryPoint_RejectsANullMessage()
    {
        await using var provider = Build();
        var engine = provider.GetRequiredService<MessageDispatchEngine>();
        var context = new ErgosfareContext();

        Assert.Throws<ArgumentNullException>(() => engine.BroadcastAsync((object) null!, provider));
        Assert.Throws<ArgumentNullException>(() => engine.BroadcastAsync<BaseNotice>(null!, provider));
        Assert.Throws<ArgumentNullException>(() => engine.BroadcastAsync((object) null!, context, provider));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task Engine_ExposesTheContainersOwnDependenciesFactory()
    {
        await using var provider = Build();
        var engine = provider.GetRequiredService<MessageDispatchEngine>();

        // The broadcast fast lane reads the factory off the engine rather than resolving
        // its own, so it has to be this container's — an executor built on another
        // container's factory would resolve handlers from the wrong scope.
        Assert.Same(provider.GetRequiredService<IMessageDependenciesFactory>(), engine.DependenciesFactory);
    }
}
