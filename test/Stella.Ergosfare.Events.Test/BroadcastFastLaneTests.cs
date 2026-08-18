using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Events.Abstractions;
using Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Events.Test;

// The fixtures live at the top level so the source generator compiles their broadcast
// plans — nothing is dispatched at run time that was not produced at compile time. Every
// type is owned by BroadcastFastLaneTests alone, and each container below registers its
// events' full compiled pipelines.

public sealed class FastLaneEvent : IEvent { public string? Tag { get; init; } }

public sealed class FastLaneEventHandler : IEventHandler<FastLaneEvent>
{
    public ValueTask HandleAsync(FastLaneEvent @event, ErgosfareContext context)
    {
        context.Set("writtenByHandler", @event.Tag ?? "-");
        return ValueTask.CompletedTask;
    }
}

public sealed class FastLaneSecondHandler : IEventHandler<FastLaneEvent>
{
    public ValueTask HandleAsync(FastLaneEvent @event, ErgosfareContext context)
    {
        context.Set("secondHandlerRan", true);
        return ValueTask.CompletedTask;
    }
}

public sealed class RewrittenEvent : IEvent { public string Payload { get; init; } = "original"; }

public sealed class RewritingPreInterceptor : IEventPreInterceptor<RewrittenEvent>
{
    public ValueTask<RewrittenEvent> HandleAsync(RewrittenEvent @event, ErgosfareContext context)
        => ValueTask.FromResult(new RewrittenEvent { Payload = @event.Payload + "+rewritten" });
}

public sealed class RewrittenEventHandler : IEventHandler<RewrittenEvent>
{
    public ValueTask HandleAsync(RewrittenEvent @event, ErgosfareContext context)
    {
        context.Set("observedPayload", @event.Payload);
        context.Set("observedInstance", @event);
        return ValueTask.CompletedTask;
    }
}

public sealed class ThrowingEvent : IEvent { }

public sealed class ThrowingEventHandler : IEventHandler<ThrowingEvent>
{
    public ValueTask HandleAsync(ThrowingEvent @event, ErgosfareContext context)
        => throw new InvalidOperationException("evt-boom");
}

/// <summary>
/// An event no handler anywhere subscribes to. Nothing participates, so the generator
/// emits no composition for it and a publish reaches nobody — silently.
/// </summary>
public sealed class HandlerlessEvent : IEvent { }

public sealed class SlowEvent : IEvent { }

public sealed class SlowEventHandler : IEventHandler<SlowEvent>
{
    public async ValueTask HandleAsync(SlowEvent @event, ErgosfareContext context)
    {
        await Task.Delay(10);
        context.Set("afterAwait", 42);
    }
}

public sealed class OuterEvent : IEvent { }
public sealed class InnerEvent : IEvent { }

public sealed class InnerEventHandler : IEventHandler<InnerEvent>
{
    public ValueTask HandleAsync(InnerEvent @event, ErgosfareContext context)
    {
        context.Set("innerRan", true);
        return ValueTask.CompletedTask;
    }
}

public sealed class OuterEventHandler(IEventMediator events) : IEventHandler<OuterEvent>
{
    public async ValueTask HandleAsync(OuterEvent @event, ErgosfareContext context)
    {
        using var scope = context.CreateScope();
        await events.PublishAsync(new InnerEvent(), scope.Context);
        context.Set("outerSawInner", scope.Context.Has("innerRan"));
    }
}

public sealed class ScopedProbe { }

public sealed class ScopedEvent : IEvent { }

public sealed class ScopedEventHandler(ScopedProbe probe) : IEventHandler<ScopedEvent>
{
    public ValueTask HandleAsync(ScopedEvent @event, ErgosfareContext context)
    {
        context.Set("probe", probe);
        return ValueTask.CompletedTask;
    }
}

public sealed class IsolatedEvent : IEvent { }

public sealed class IsolatedEventHandler : IEventHandler<IsolatedEvent>
{
    public ValueTask HandleAsync(IsolatedEvent @event, ErgosfareContext context)
    {
        context.Set("handlerInstance", this);
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Covers publishing through the compiled broadcast plans: pooled execution contexts with
/// caller-owned items semantics, group-less delivery, the pre-interceptor message
/// transformation, and the loud failure of a pipeline no plan can serve.
/// </summary>
public class BroadcastFastLaneTests
{
    private static ServiceProvider Build(Action<EventModuleBuilder>? extra = null)
        => new ServiceCollection()
            .AddErgosfare(x => x.AddEventModule(e =>
            {
                e.Register<FastLaneEventHandler>();
                e.Register<FastLaneSecondHandler>();
                extra?.Invoke(e);
            }))
            .BuildServiceProvider();

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task Publish_ShouldMakeHandlerWrites_VisibleInCallerSettingsItems()
    {
        await using var provider = Build();
        var mediator = provider.GetRequiredService<IEventMediator>();
        var settings = new ErgosfareContext();

        await mediator.PublishAsync(new FastLaneEvent { Tag = "t1" }, settings);

        Assert.Equal("t1", settings.Items["writtenByHandler"]);
        Assert.Equal(true, settings.Items["secondHandlerRan"]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task Publish_ShouldKeepCallerSeededItems_AndSurviveTheDispatch()
    {
        await using var provider = Build();
        var mediator = provider.GetRequiredService<IEventMediator>();
        var settings = new ErgosfareContext();
        settings.Items["seed"] = "hello";

        await mediator.PublishAsync(new FastLaneEvent(), settings);

        // The dictionary is adopted for the dispatch and detached on return — never wiped.
        Assert.Equal("hello", settings.Items["seed"]);
        Assert.True(settings.Items.ContainsKey("writtenByHandler"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task Publish_WithDefaultSettings_ShouldStartFromACleanPooledContext()
    {
        await using var provider = Build();
        var mediator = provider.GetRequiredService<IEventMediator>();

        // First publish seeds a caller dictionary through the pooled context.
        var settings = new ErgosfareContext();
        settings.Items["seed"] = "hello";
        await mediator.PublishAsync(new FastLaneEvent(), settings);

        // A default-settings publish right after must not observe any of it, and must not
        // pollute the earlier caller's dictionary either.
        var probe = new ErgosfareContext();
        await mediator.PublishAsync(new FastLaneEvent { Tag = "probe" }, probe);

        Assert.False(probe.Items.ContainsKey("seed"));
        Assert.Equal("probe", probe.Items["writtenByHandler"]);
        Assert.Equal("hello", settings.Items["seed"]);
        Assert.NotEqual("probe", settings.Items["writtenByHandler"]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task Publish_ShouldContinueWithThePreInterceptorsReturnedEvent()
    {
        await using var provider = Build(e =>
        {
            e.Register<RewritingPreInterceptor>();
            e.Register<RewrittenEventHandler>();
        });
        var mediator = provider.GetRequiredService<IEventMediator>();
        var settings = new ErgosfareContext();
        var original = new RewrittenEvent();

        await mediator.PublishAsync(original, settings);

        // The handler received the brand-new instance the pre-interceptor returned.
        Assert.Equal("original+rewritten", settings.Items["observedPayload"]);
        Assert.NotSame(original, settings.Items["observedInstance"]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task Publish_ShouldPropagateHandlerException_WhenNoExceptionInterceptorsExist()
    {
        await using var provider = Build(e => e.Register<ThrowingEventHandler>());
        var mediator = provider.GetRequiredService<IEventMediator>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await mediator.PublishAsync(new ThrowingEvent()));

        Assert.Equal("evt-boom", exception.Message);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task Publish_OfAHandlerlessEvent_IsSilent()
    {
        await using var provider = Build(e => e.Register<HandlerlessEvent>());
        var mediator = provider.GetRequiredService<IEventMediator>();

        // Silent, and unconditionally so: nothing in the compilation participates in this
        // event, so it has no composition and a publish reaches nobody.
        Assert.Null(await Record.ExceptionAsync(
            async () => await mediator.PublishAsync(new HandlerlessEvent())));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task Publish_WithGenuinelyAsyncHandler_ShouldCompleteAndExposeItems()
    {
        await using var provider = Build(e => e.Register<SlowEventHandler>());
        var mediator = provider.GetRequiredService<IEventMediator>();
        var settings = new ErgosfareContext();

        await mediator.PublishAsync(new SlowEvent(), settings);

        Assert.Equal(42, settings.Items["afterAwait"]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task Publish_Nested_ShouldRunUnderTheCallerOwnedChildContext()
    {
        await using var provider = Build(e =>
        {
            e.Register<OuterEventHandler>();
            e.Register<InnerEventHandler>();
        });
        var mediator = provider.GetRequiredService<IEventMediator>();
        var settings = new ErgosfareContext();

        await mediator.PublishAsync(new OuterEvent(), settings);

        Assert.Equal(true, settings.Items["outerSawInner"]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task Publish_InParallel_ShouldNeverMixPerPublishItems()
    {
        await using var provider = Build();
        var mediator = provider.GetRequiredService<IEventMediator>();
        var mismatches = 0;

        await Task.WhenAll(Enumerable.Range(0, 4).Select(lane => Task.Run(async () =>
        {
            for (var i = 0; i < 2_000; i++)
            {
                var settings = new ErgosfareContext();
                var tag = $"{lane}:{i}";

                await mediator.PublishAsync(new FastLaneEvent { Tag = tag }, settings);

                if (!Equals(settings.Items["writtenByHandler"], tag))
                {
                    Interlocked.Increment(ref mismatches);
                }
            }
        })));

        Assert.Equal(0, mismatches);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task Publish_WithASingletonSubscriber_DeliversTheOneInstanceEveryTime()
    {
        // A singleton-registered handler makes the pipeline memoized underneath, but only
        // demanded memoization (ForceMemoizedHandlers) bars a compiled plan: the plan's
        // resolving variant returns the one singleton per publish, which is exactly what
        // memoization promises, so the publish runs — and every delivery is the same
        // instance.
        await using var provider = new ServiceCollection()
            .AddSingleton<IsolatedEventHandler>()
            .AddErgosfare(x => x.AddEventModule(e => e.Register<IsolatedEventHandler>()))
            .BuildServiceProvider();

        var mediator = provider.GetRequiredService<IEventMediator>();

        var firstContext = new ErgosfareContext();
        await mediator.PublishAsync(new IsolatedEvent(), firstContext);

        var secondContext = new ErgosfareContext();
        await mediator.PublishAsync(new IsolatedEvent(), secondContext);

        var delivered = Assert.IsType<IsolatedEventHandler>(firstContext.Get<object>("handlerInstance"));
        Assert.Same(delivered, secondContext.Get<object>("handlerInstance"));
        Assert.Same(delivered, provider.GetRequiredService<IsolatedEventHandler>());
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task Publish_FromAScope_ShouldResolveHandlersAgainstThatScope()
    {
        var provider = new ServiceCollection()
            .AddScoped<ScopedProbe>()
            .AddErgosfare(x => x.AddEventModule(e => e.Register<ScopedEventHandler>()))
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        await using var _ = provider;

        using var scope = provider.CreateScope();
        var expected = scope.ServiceProvider.GetRequiredService<ScopedProbe>();
        var settings = new ErgosfareContext();

        await scope.ServiceProvider.GetRequiredService<IEventMediator>()
            .PublishAsync(new ScopedEvent(), settings);

        Assert.Same(expected, settings.Items["probe"]);
    }

    /// <summary>
    /// Kept nested and excluded on purpose: the generator never sees a pipeline for it, so
    /// no composition and no plan exist — the exact shape of an event nobody ever wired up.
    /// </summary>
    [ExcludeFromDiscovery]
    public sealed class NeverRegisteredEvent : IEvent { }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task Publish_OfANeverRegisteredType_IsSilent()
    {
        await using var provider = Build();

        var mediator = provider.GetRequiredService<IEventMediator>();

        // Silent, exactly as for a registered event nobody handles: no composition serves
        // the type, so the publish reaches nobody and reaching nobody is not an error.
        await mediator.PublishAsync(new NeverRegisteredEvent());
        Assert.Null(await Record.ExceptionAsync(
            async () => await mediator.PublishAsync(new NeverRegisteredEvent())));
    }
}
