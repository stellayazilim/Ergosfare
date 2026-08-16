using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Core.Abstractions.DispatchRoots;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Events.Abstractions;
using Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection;

namespace Stella.Ergosfare.SourceGenerator.Test;

/// <summary>
///     The filtering plan: the body that serves a dispatch whose group set is a runtime
///     value. Every participant is present and each call sits behind the group test the
///     generator baked for it, so one body answers any set — including sets no call site
///     ever spelled.
/// </summary>
/// <remarks>
///     The interceptor is what proves the guards work on more than the handler segment: it
///     is declared in one group, so a publish naming the other group must reach that group's
///     handler without running it.
/// </remarks>
public class FilteringPlanExecutionTests
{
    private const string Source = """
        using System.Collections.Generic;
        using System.Threading.Tasks;
        using Stella.Ergosfare.Core.Abstractions;
        using Stella.Ergosfare.Core.Abstractions.Attributes;
        using Stella.Ergosfare.Events.Abstractions;

        namespace TestApp
        {
            public static class FilterSink
            {
                public static readonly List<string> Entries = new List<string>();
            }

            public sealed class GenFilteredEvent : IEvent { }

            [Group("audit")]
            public sealed class GenAuditHandler : IEventHandler<GenFilteredEvent>
            {
                public ValueTask HandleAsync(GenFilteredEvent @event, ErgosfareContext context)
                {
                    FilterSink.Entries.Add("audit");
                    return default;
                }
            }

            [Group("billing")]
            public sealed class GenBillingHandler : IEventHandler<GenFilteredEvent>
            {
                public ValueTask HandleAsync(GenFilteredEvent @event, ErgosfareContext context)
                {
                    FilterSink.Entries.Add("billing");
                    return default;
                }
            }

            public sealed class GenDefaultHandler : IEventHandler<GenFilteredEvent>
            {
                public ValueTask HandleAsync(GenFilteredEvent @event, ErgosfareContext context)
                {
                    FilterSink.Entries.Add("default");
                    return default;
                }
            }

            [Group("audit")]
            public sealed class GenAuditPre : IEventPreInterceptor<GenFilteredEvent>
            {
                public ValueTask<GenFilteredEvent> HandleAsync(GenFilteredEvent @event, ErgosfareContext context)
                {
                    FilterSink.Entries.Add("audit-pre");
                    return ValueTask.FromResult(@event);
                }
            }

            public static class FilterCaller
            {
                // The set is a parameter, so no call site proves it — this is exactly the
                // shape the filtering plan exists for.
                public static ValueTask Publish(IEventMediator mediator, GenFilteredEvent @event, string[] groups)
                    => mediator.PublishAsync(@event, groups, System.Threading.CancellationToken.None);
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

        var provider = new ServiceCollection()
            .AddErgosfare(options => options.AddEventModule(events => registerEvents.Invoke(null, [events])))
            .BuildServiceProvider();

        return (assembly, provider);
    });

    private static List<string> Entries
        => (List<string>)Host.Value.Assembly.GetType("TestApp.FilterSink", throwOnError: true)!
            .GetField("Entries", BindingFlags.Public | BindingFlags.Static)!
            .GetValue(null)!;

    private static async Task<List<string>> PublishAsync(params string[] groups)
    {
        var (assembly, provider) = Host.Value;

        Entries.Clear();

        var @event = (IEvent)Activator.CreateInstance(
            assembly.GetType("TestApp.GenFilteredEvent", throwOnError: true)!)!;

        await provider.GetRequiredService<IEventMediator>().PublishAsync(@event, groups, System.Threading.CancellationToken.None);

        return [.. Entries];
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void UnreadableFilter_ProducesTheFilteringPlan()
    {
        var (assembly, _) = Host.Value;
        var eventType = assembly.GetType("TestApp.GenFilteredEvent", throwOnError: true)!;

        var filtering = GeneratedDispatchRoots.FindFilteredBroadcastPlan(eventType);

        Assert.NotNull(filtering);

        // It covers every group its participants declare — the set the gate validates it
        // against, and the reason one body can answer any request.
        Assert.Equal(["audit", "billing", "default"], filtering.FilterGroups!);

        // And no set is keyed, because no call site proved one.
        Assert.Null(GeneratedDispatchRoots.FindBroadcastPlan(eventType, ["audit"]));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task FilteringPlan_RunsOnlyTheRequestedGroup()
    {
        Assert.Equal(["audit-pre", "audit"], await PublishAsync("audit"));

        // The audit interceptor is guarded too: a billing publish reaches billing's handler
        // and nothing else.
        Assert.Equal(["billing"], await PublishAsync("billing"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task FilteringPlan_HonorsMultiGroupAndDefaultRequests()
    {
        Assert.Equal(["audit-pre", "audit", "billing"], await PublishAsync("audit", "billing"));

        // An empty request is the default group — the ungrouped handler alone.
        Assert.Equal(["default"], await PublishAsync());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task FilteringPlan_DeliversNothingForAnUnknownGroup()
    {
        Assert.Empty(await PublishAsync("nobody-declares-this"));
    }
}
