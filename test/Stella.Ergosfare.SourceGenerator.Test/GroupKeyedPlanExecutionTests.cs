using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.DispatchRoots;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Events.Abstractions;
using Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection;

namespace Stella.Ergosfare.SourceGenerator.Test;

/// <summary>
///     A group-keyed plan executed end to end: the app source compiles with the generator,
///     loads into the test process, wires a real container, and publishes under the same
///     group set its call site named.
/// </summary>
/// <remarks>
///     Two proofs are wanted here and they are different. That the filtered publish reaches
///     exactly the filtered handlers is <em>correctness</em>, and the runtime group lane
///     would satisfy it too. That the plan for the set exists and is the thing that ran is
///     the point of the feature — so the test asserts the store holds a plan for the set,
///     and that the default set's plan is a different one.
/// </remarks>
public class GroupKeyedPlanExecutionTests
{
    private const string Source = """
        using System.Collections.Generic;
        using System.Threading.Tasks;
        using Stella.Ergosfare.Core.Abstractions;
        using Stella.Ergosfare.Core.Abstractions.Attributes;
        using Stella.Ergosfare.Events.Abstractions;

        namespace TestApp
        {
            public static class GroupSink
            {
                public static readonly List<string> Entries = new List<string>();
            }

            public sealed class GenGroupedEvent : IEvent { }

            [Group("audit")]
            public sealed class GenAuditHandler : IEventHandler<GenGroupedEvent>
            {
                public ValueTask HandleAsync(GenGroupedEvent @event, ErgosfareContext context)
                {
                    GroupSink.Entries.Add("audit");
                    return default;
                }
            }

            [Group("audit")]
            public sealed class GenSecondAuditHandler : IEventHandler<GenGroupedEvent>
            {
                public ValueTask HandleAsync(GenGroupedEvent @event, ErgosfareContext context)
                {
                    GroupSink.Entries.Add("audit2");
                    return default;
                }
            }

            [Group("billing")]
            public sealed class GenBillingHandler : IEventHandler<GenGroupedEvent>
            {
                public ValueTask HandleAsync(GenGroupedEvent @event, ErgosfareContext context)
                {
                    GroupSink.Entries.Add("billing");
                    return default;
                }
            }

            public sealed class GenDefaultHandler : IEventHandler<GenGroupedEvent>
            {
                public ValueTask HandleAsync(GenGroupedEvent @event, ErgosfareContext context)
                {
                    GroupSink.Entries.Add("default");
                    return default;
                }
            }

            public static class GroupCaller
            {
                public static readonly GroupSet Audit = GroupSet.Of("audit");

                public static ValueTask Publish(IEventMediator mediator, GenGroupedEvent @event)
                    => mediator.PublishAsync(@event, Audit);
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
        => (List<string>)Host.Value.Assembly.GetType("TestApp.GroupSink", throwOnError: true)!
            .GetField("Entries", BindingFlags.Public | BindingFlags.Static)!
            .GetValue(null)!;

    [Fact]
    [Trait("Category", "Unit")]
    public void ProvenGroupSet_HasItsOwnPlanInTheStore()
    {
        var (assembly, _) = Host.Value;
        var eventType = assembly.GetType("TestApp.GenGroupedEvent", throwOnError: true)!;

        var audit = GeneratedDispatchRoots.FindBroadcastPlan(eventType, ["audit"]);
        var @default = GeneratedDispatchRoots.FindBroadcastPlan(eventType);

        // The set the call site named is keyed; the default set is a different pipeline and
        // therefore a different plan.
        Assert.NotNull(audit);
        Assert.NotNull(@default);
        Assert.NotSame(@default, audit);

        // A set nobody dispatches under is not keyed — plans follow call sites, not the
        // cartesian product of the groups a composition happens to declare.
        Assert.Null(GeneratedDispatchRoots.FindBroadcastPlan(eventType, ["billing"]));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task FilteredPublish_DeliversToExactlyTheFilteredHandlers()
    {
        var (assembly, provider) = Host.Value;

        Entries.Clear();

        var @event = (IEvent)Activator.CreateInstance(
            assembly.GetType("TestApp.GenGroupedEvent", throwOnError: true)!)!;

        await provider.GetRequiredService<IEventMediator>().PublishAsync(@event, GroupSet.Of("audit"));

        // Both audit handlers, in ordinal order, and nobody else — the ungrouped handler
        // belongs to the default group, which this publish did not ask for.
        Assert.Equal(["audit", "audit2"], Entries);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task DefaultPublish_DeliversToTheUngroupedHandlerAlone()
    {
        var (assembly, provider) = Host.Value;

        Entries.Clear();

        var @event = (IEvent)Activator.CreateInstance(
            assembly.GetType("TestApp.GenGroupedEvent", throwOnError: true)!)!;

        await provider.GetRequiredService<IEventMediator>().PublishAsync(@event);

        Assert.Equal(["default"], Entries);
    }

    /// <summary>
    ///     Group selection is an any-of test, so the spelling a dispatch happens to use must
    ///     find the plan baked for that set — a key that honored order would miss it.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void GroupSetKey_IgnoresOrderAndRepetition()
    {
        var (assembly, _) = Host.Value;
        var eventType = assembly.GetType("TestApp.GenGroupedEvent", throwOnError: true)!;

        Assert.NotNull(GeneratedDispatchRoots.FindBroadcastPlan(eventType, ["audit", "audit"]));
    }
}
