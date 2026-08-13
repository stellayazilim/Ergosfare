using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Abstractions.Strategies;
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

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task Publish_ShouldMakeHandlerWrites_VisibleInCallerSettingsItems()
    {
        await using var provider = Build();
        var mediator = provider.GetRequiredService<IEventMediator>();
        var settings = new Dictionary<object, object?>();

        await mediator.PublishAsync(new FastLaneEvent { Tag = "t1" }, settings);

        Assert.Equal("t1", settings["writtenByHandler"]);
        Assert.Equal(true, settings["secondHandlerRan"]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task Publish_ShouldKeepCallerSeededItems_AndSurviveTheDispatch()
    {
        await using var provider = Build();
        var mediator = provider.GetRequiredService<IEventMediator>();
        var settings = new Dictionary<object, object?>();
        settings["seed"] = "hello";

        await mediator.PublishAsync(new FastLaneEvent(), settings);

        // The dictionary is adopted for the dispatch and detached on return — never wiped.
        Assert.Equal("hello", settings["seed"]);
        Assert.True(settings.ContainsKey("writtenByHandler"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task Publish_WithDefaultSettings_ShouldStartFromACleanPooledContext()
    {
        await using var provider = Build();
        var mediator = provider.GetRequiredService<IEventMediator>();

        // First publish seeds a caller dictionary through the pooled context.
        var settings = new Dictionary<object, object?>();
        settings["seed"] = "hello";
        await mediator.PublishAsync(new FastLaneEvent(), settings);

        // A default-settings publish right after must not observe any of it, and must not
        // pollute the earlier caller's dictionary either.
        var probe = new Dictionary<object, object?>();
        await mediator.PublishAsync(new FastLaneEvent { Tag = "probe" }, probe);

        Assert.False(probe.ContainsKey("seed"));
        Assert.Equal("probe", probe["writtenByHandler"]);
        Assert.Equal("hello", settings["seed"]);
        Assert.NotEqual("probe", settings["writtenByHandler"]);
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
        var settings = new Dictionary<object, object?>();
        var original = new RewrittenEvent();

        await mediator.PublishAsync(original, settings);

        // The handler received the brand-new instance the pre-interceptor returned.
        Assert.Equal("original+rewritten", settings["observedPayload"]);
        Assert.NotSame(original, settings["observedInstance"]);
    }

    public sealed class ThrowingEvent : IEvent { }

    public sealed class ThrowingEventHandler : IEventHandler<ThrowingEvent>
    {
        public ValueTask HandleAsync(ThrowingEvent @event, ErgosfareContext context)
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
                new HandlerlessEvent(), groups: null, items: null, throwIfNoHandlerFound: true, CancellationToken.None));
    }

    public sealed class SlowEvent : IEvent { }

    public sealed class SlowEventHandler : IEventHandler<SlowEvent>
    {
        public async ValueTask HandleAsync(SlowEvent @event, ErgosfareContext context)
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
        var settings = new Dictionary<object, object?>();

        await mediator.PublishAsync(new SlowEvent(), settings);

        Assert.Equal(42, settings["afterAwait"]);
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
        var settings = new Dictionary<object, object?>();

        await mediator.PublishAsync(new OuterEvent(), settings);

        Assert.Equal(true, settings["outerSawInner"]);
    }

    public sealed class LateEvent : IEvent { }

    /// <summary>
    /// Excluded from discovery: the fact below registers this type at runtime to observe
    /// the version bump — another test's assembly scan (the registry is process-wide)
    /// must not slip it into the pipeline before the warm publishes run.
    /// </summary>
    [ExcludeFromDiscovery]
    public sealed class LateEventHandler : IEventHandler<LateEvent>
    {
        public ValueTask HandleAsync(LateEvent @event, ErgosfareContext context)
        {
            context.Set("lateHandlerRan", true);
            return ValueTask.CompletedTask;
        }
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
                var settings = new Dictionary<object, object?>();
                var tag = $"{lane}:{i}";

                await mediator.PublishAsync(new FastLaneEvent { Tag = tag }, settings);

                if (!Equals(settings["writtenByHandler"], tag))
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

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task Publish_WithMemoizedPipeline_ShouldNeverServeAnotherContainersPlan()
    {
        // Singleton-registered handler => memoized pipeline, whose plan pins the creating
        // container's root provider. The process-wide invoker must key its cached plan by
        // factory, so two containers publishing the same event type each hit their own
        // handler instance.
        static ServiceProvider BuildContainer() => new ServiceCollection()
            .AddSingleton<IsolatedEventHandler>()
            .AddErgosfare(x => x.AddEventModule(e => e.Register<IsolatedEventHandler>()))
            .BuildServiceProvider();

        await using var first = BuildContainer();
        await using var second = BuildContainer();

        var firstInstance = first.GetRequiredService<IsolatedEventHandler>();
        var secondInstance = second.GetRequiredService<IsolatedEventHandler>();
        Assert.NotSame(firstInstance, secondInstance);

        var settings = new Dictionary<object, object?>();
        await first.GetRequiredService<IEventMediator>().PublishAsync(new IsolatedEvent(), settings);
        Assert.Same(firstInstance, settings["handlerInstance"]);

        settings = new Dictionary<object, object?>();
        await second.GetRequiredService<IEventMediator>().PublishAsync(new IsolatedEvent(), settings);
        Assert.Same(secondInstance, settings["handlerInstance"]);

        settings = new Dictionary<object, object?>();
        await first.GetRequiredService<IEventMediator>().PublishAsync(new IsolatedEvent(), settings);
        Assert.Same(firstInstance, settings["handlerInstance"]);
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
        var settings = new Dictionary<object, object?>();

        await scope.ServiceProvider.GetRequiredService<IEventMediator>()
            .PublishAsync(new ScopedEvent(), settings);

        Assert.Same(expected, settings["probe"]);
    }

    [ExcludeFromDiscovery]
    public sealed class NeverRegisteredEvent : IEvent { }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task Publish_ShouldHonorThrowIfNoHandlerFound_ForAnUnregisteredType()
    {
        await using var provider = Build();

        var mediator = provider.GetRequiredService<IEventMediator>();

        // Default: silent, exactly as for a registered event nobody handles.
        await mediator.PublishAsync(new NeverRegisteredEvent());

        await Assert.ThrowsAsync<NoHandlerFoundException>(async () =>
            await mediator.PublishAsync(
                new NeverRegisteredEvent(), groups: null, items: null, throwIfNoHandlerFound: true, CancellationToken.None));
    }
}
