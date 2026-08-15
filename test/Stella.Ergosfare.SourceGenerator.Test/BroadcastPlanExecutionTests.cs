using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Core.Abstractions.DispatchRoots;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Events.Abstractions;
using Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection;

namespace Stella.Ergosfare.SourceGenerator.Test;

/// <summary>
///     The broadcast plan executed end to end: the app source below compiles with the
///     generator, loads into the test process, wires a real container, and publishes through
///     the public event mediator.
/// </summary>
/// <remarks>
///     The plugin observer is the proof the plan ran. It exists only inside the emitted plan
///     body — the runtime strategy has no idea it exists — so its entries in the sink mean
///     the publish took the plan lane and not the strategy. That it appears once per handler
///     is the other half: the pre- and post-handler boundaries name the seam around a
///     delivery, and a broadcast has one per handler.
/// </remarks>
public class BroadcastPlanExecutionTests
{
    private const string Source = """
        using System.Collections.Generic;
        using System.Threading.Tasks;
        using Stella.Ergosfare.Core.Abstractions;
        using Stella.Ergosfare.Events.Abstractions;
        using Stella.Ergosfare.Plugins.Abstractions;

        namespace TestApp
        {
            public static class BroadcastSink
            {
                public static readonly List<string> Entries = new List<string>();
            }

            public interface IGenDomainEvent : IEvent { }

            public sealed class GenOrderPlaced : IGenDomainEvent
            {
                public string Tag { get; set; } = "original";
            }

            public sealed class GenBroadcastAuditHandler : IEventHandler<GenOrderPlaced>
            {
                public ValueTask HandleAsync(GenOrderPlaced @event, ErgosfareContext context)
                {
                    BroadcastSink.Entries.Add("audit:" + @event.Tag);
                    return default;
                }
            }

            public sealed class GenBroadcastNotifyHandler : IEventHandler<GenOrderPlaced>
            {
                public ValueTask HandleAsync(GenOrderPlaced @event, ErgosfareContext context)
                {
                    BroadcastSink.Entries.Add("notify:" + @event.Tag);
                    return default;
                }
            }

            // Registered against the base event: the covariant segment, delivered after the
            // direct one.
            public sealed class GenBroadcastCatchAllHandler : IEventHandler<IGenDomainEvent>
            {
                public ValueTask HandleAsync(IGenDomainEvent @event, ErgosfareContext context)
                {
                    BroadcastSink.Entries.Add("catchall");
                    return default;
                }
            }

            public sealed class GenBroadcastPre : IEventPreInterceptor<GenOrderPlaced>
            {
                public ValueTask<GenOrderPlaced> HandleAsync(GenOrderPlaced @event, ErgosfareContext context)
                {
                    BroadcastSink.Entries.Add("pre:" + @event.Tag);
                    @event.Tag = "rewritten";
                    return ValueTask.FromResult(@event);
                }
            }

            public sealed class GenBroadcastFinal : IEventFinalInterceptor<GenOrderPlaced>
            {
                public ValueTask HandleAsync(
                    GenOrderPlaced @event, System.Exception? exception, ErgosfareContext context)
                {
                    BroadcastSink.Entries.Add("final");
                    return default;
                }
            }

            public sealed class GenBroadcastHooks
            {
                [PipelineInvokable(Hook.PostMain)]
                public void Delivered<TMessage>(TMessage message, ErgosfareContext context)
                    => BroadcastSink.Entries.Add("observer");
            }
        }
        """;

    private static readonly Lazy<(Assembly Assembly, ServiceProvider Provider)> Host = new(() =>
    {
        var result = GeneratorTestHost.Run(Source);

        Assert.Empty(result.CompilationErrors);

        using var stream = new MemoryStream();
        Assert.True(result.OutputCompilation.Emit(stream).Success);

        var assembly = Assembly.Load(stream.ToArray());
        var registrations = assembly.GetType(
            "Stella.Ergosfare.Generated.ErgosfareGeneratedRegistrations", throwOnError: true)!;
        var registerEvents = registrations.GetMethod("RegisterGenerated", [typeof(EventModuleBuilder)])!;
        var hooks = assembly.GetType("TestApp.GenBroadcastHooks", throwOnError: true)!;

        var services = new ServiceCollection();
        services.AddSingleton(hooks);

        var provider = services
            .AddErgosfare(options => options.AddEventModule(events => registerEvents.Invoke(null, [events])))
            .BuildServiceProvider();

        return (assembly, provider);
    });

    private static List<string> Entries
        => (List<string>)Host.Value.Assembly.GetType("TestApp.BroadcastSink", throwOnError: true)!
            .GetField("Entries", BindingFlags.Public | BindingFlags.Static)!
            .GetValue(null)!;

    [Fact]
    [Trait("Category", "Unit")]
    public async Task InterceptedBroadcast_RunsThePlanAndDeliversToEveryHandler()
    {
        var (assembly, provider) = Host.Value;

        Assert.NotNull(GeneratedDispatchRoots.FindBroadcastPlan(assembly.GetType("TestApp.GenOrderPlaced")!));

        Entries.Clear();

        var @event = (IEvent)Activator.CreateInstance(
            assembly.GetType("TestApp.GenOrderPlaced", throwOnError: true)!)!;

        await provider.GetRequiredService<IEventMediator>().PublishAsync(@event);

        // The pre stage rewrites the event and every handler sees the rewritten instance;
        // the direct segment runs in ordinal order, then the covariant one, then the final
        // stage. The observer lands after each delivery — including the covariant one.
        Assert.Equal(
        [
            "pre:original",
            "audit:rewritten", "observer",
            "notify:rewritten", "observer",
            "catchall", "observer",
            "final",
        ], Entries);
    }
}
