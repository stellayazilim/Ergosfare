using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Abstractions.Registry;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Events.Abstractions;
using Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Events.Test;

/// <summary>
/// Covers the broadcast fast lane: pooled execution contexts with caller-owned items
/// semantics, the cached default-settings strategy, the invoker-cached pipeline plan and
/// its registry-version invalidation, and the pre-interceptor message transformation.
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

    public sealed class FastLaneEvent : IEvent { public string? Tag { get; init; } }

    public sealed class FastLaneEventHandler : IEventHandler<FastLaneEvent>
    {
        public ValueTask HandleAsync(FastLaneEvent @event, IExecutionContext context)
        {
            context.Set("writtenByHandler", @event.Tag ?? "-");
            return ValueTask.CompletedTask;
        }
    }

    public sealed class FastLaneSecondHandler : IEventHandler<FastLaneEvent>
    {
        public ValueTask HandleAsync(FastLaneEvent @event, IExecutionContext context)
        {
            context.Set("secondHandlerRan", true);
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task Publish_ShouldMakeHandlerWrites_VisibleInCallerSettingsItems()
    {
        await using var provider = Build();
        var mediator = provider.GetRequiredService<IEventMediator>();
        var settings = new EventMediationSettings();

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
        var settings = new EventMediationSettings();
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
        var settings = new EventMediationSettings();
        settings.Items["seed"] = "hello";
        await mediator.PublishAsync(new FastLaneEvent(), settings);

        // A default-settings publish right after must not observe any of it, and must not
        // pollute the earlier caller's dictionary either.
        var probe = new EventMediationSettings();
        await mediator.PublishAsync(new FastLaneEvent { Tag = "probe" }, probe);

        Assert.False(probe.Items.ContainsKey("seed"));
        Assert.Equal("probe", probe.Items["writtenByHandler"]);
        Assert.Equal("hello", settings.Items["seed"]);
        Assert.NotEqual("probe", settings.Items["writtenByHandler"]);
    }

    public sealed class RewrittenEvent : IEvent { public string Payload { get; init; } = "original"; }

    public sealed class RewritingPreInterceptor : IEventPreInterceptor<RewrittenEvent>
    {
        public ValueTask<RewrittenEvent> HandleAsync(RewrittenEvent @event, IExecutionContext context)
            => ValueTask.FromResult(new RewrittenEvent { Payload = @event.Payload + "+rewritten" });
    }

    public sealed class RewrittenEventHandler : IEventHandler<RewrittenEvent>
    {
        public ValueTask HandleAsync(RewrittenEvent @event, IExecutionContext context)
        {
            context.Set("observedPayload", @event.Payload);
            context.Set("observedInstance", @event);
            return ValueTask.CompletedTask;
        }
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
        var settings = new EventMediationSettings();
        var original = new RewrittenEvent();

        await mediator.PublishAsync(original, settings);

        // The handler received the brand-new instance the pre-interceptor returned.
        Assert.Equal("original+rewritten", settings.Items["observedPayload"]);
        Assert.NotSame(original, settings.Items["observedInstance"]);
    }

    public sealed class ThrowingEvent : IEvent { }

    public sealed class ThrowingEventHandler : IEventHandler<ThrowingEvent>
    {
        public ValueTask HandleAsync(ThrowingEvent @event, IExecutionContext context)
            => throw new InvalidOperationException("evt-boom");
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

    public sealed class HandlerlessEvent : IEvent { }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task Publish_ShouldHonorThrowIfNoHandlerFound()
    {
        await using var provider = Build(e => e.Register<HandlerlessEvent>());
        var mediator = provider.GetRequiredService<IEventMediator>();

        // Default: silent.
        await mediator.PublishAsync(new HandlerlessEvent());

        await Assert.ThrowsAsync<NoHandlerFoundException>(async () =>
            await mediator.PublishAsync(
                new HandlerlessEvent(),
                new EventMediationSettings { ThrowIfNoHandlerFound = true }));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task Publish_ShouldApplyHandlerPredicateFilter()
    {
        await using var provider = Build();
        var mediator = provider.GetRequiredService<IEventMediator>();
        var settings = new EventMediationSettings();
        settings.Filters.HandlerPredicate = type => type != typeof(FastLaneSecondHandler);

        await mediator.PublishAsync(new FastLaneEvent { Tag = "filtered" }, settings);

        Assert.Equal("filtered", settings.Items["writtenByHandler"]);
        Assert.False(settings.Items.ContainsKey("secondHandlerRan"));
    }

    public sealed class SlowEvent : IEvent { }

    public sealed class SlowEventHandler : IEventHandler<SlowEvent>
    {
        public async ValueTask HandleAsync(SlowEvent @event, IExecutionContext context)
        {
            await Task.Delay(10);
            context.Set("afterAwait", 42);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task Publish_WithGenuinelyAsyncHandler_ShouldCompleteAndExposeItems()
    {
        await using var provider = Build(e => e.Register<SlowEventHandler>());
        var mediator = provider.GetRequiredService<IEventMediator>();
        var settings = new EventMediationSettings();

        await mediator.PublishAsync(new SlowEvent(), settings);

        Assert.Equal(42, settings.Items["afterAwait"]);
    }

    public sealed class OuterEvent : IEvent { }
    public sealed class InnerEvent : IEvent { }

    public sealed class InnerEventHandler : IEventHandler<InnerEvent>
    {
        public ValueTask HandleAsync(InnerEvent @event, IExecutionContext context)
        {
            context.Set("innerRan", true);
            return ValueTask.CompletedTask;
        }
    }

    public sealed class OuterEventHandler(IEventMediator events) : IEventHandler<OuterEvent>
    {
        public async ValueTask HandleAsync(OuterEvent @event, IExecutionContext context)
        {
            using var scope = context.CreateScope();
            await events.PublishAsync(new InnerEvent(), scope.Context);
            context.Set("outerSawInner", scope.Context.Has("innerRan"));
        }
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
        var settings = new EventMediationSettings();

        await mediator.PublishAsync(new OuterEvent(), settings);

        Assert.Equal(true, settings.Items["outerSawInner"]);
    }

    public sealed class LateEvent : IEvent { }

    public sealed class LateEventHandler : IEventHandler<LateEvent>
    {
        public ValueTask HandleAsync(LateEvent @event, IExecutionContext context)
        {
            context.Set("lateHandlerRan", true);
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task Publish_ShouldPickUpRuntimeRegistrations_AfterWarmingTheInvokerPlan()
    {
        var provider = new ServiceCollection()
            .AddTransient<LateEventHandler>()
            .AddErgosfare(x => x.AddEventModule(e => e.Register<LateEvent>()))
            .BuildServiceProvider();
        await using var _ = provider;

        var mediator = provider.GetRequiredService<IEventMediator>();
        var registry = provider.GetRequiredService<IMessageRegistry>();

        // Warm the invoker-cached plan with zero handlers (silent publishes).
        var warm = new EventMediationSettings();
        await mediator.PublishAsync(new LateEvent(), warm);
        await mediator.PublishAsync(new LateEvent(), warm);
        Assert.False(warm.Items.ContainsKey("lateHandlerRan"));

        // A runtime registration bumps the registry version; the cached plan must rebuild.
        registry.Register(typeof(LateEventHandler));

        var probe = new EventMediationSettings();
        await mediator.PublishAsync(new LateEvent(), probe);

        Assert.Equal(true, probe.Items["lateHandlerRan"]);
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
                var settings = new EventMediationSettings();
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

    public sealed class ScopedProbe { }

    public sealed class ScopedEvent : IEvent { }

    public sealed class ScopedEventHandler(ScopedProbe probe) : IEventHandler<ScopedEvent>
    {
        public ValueTask HandleAsync(ScopedEvent @event, IExecutionContext context)
        {
            context.Set("probe", probe);
            return ValueTask.CompletedTask;
        }
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
        var settings = new EventMediationSettings();

        await scope.ServiceProvider.GetRequiredService<IEventMediator>()
            .PublishAsync(new ScopedEvent(), settings);

        Assert.Same(expected, settings.Items["probe"]);
    }
}
