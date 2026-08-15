namespace Stella.Ergosfare.SourceGenerator.Test;

/// <summary>
///     Group-keyed plan emission: a dispatch names a group set, and the set is part of what
///     decides the pipeline — so it is part of what keys the plan. The generator reads the
///     sets its call sites prove and bakes one plan per (message, set); a set it cannot read
///     keys nothing and leaves that dispatch to the runtime group lane.
/// </summary>
public class GroupKeyedPlanEmissionTests
{
    private const string GroupedPipeline = """
        using Stella.Ergosfare.Core.Abstractions;
        using Stella.Ergosfare.Core.Abstractions.Attributes;
        using Stella.Ergosfare.Events.Abstractions;
        using System.Threading;
        using System.Threading.Tasks;

        namespace TestApp
        {
            public sealed record OrderPlaced : IEvent;

            [Group("audit")]
            public sealed class AuditHandler : IEventHandler<OrderPlaced>
            {
                public ValueTask HandleAsync(OrderPlaced message, ErgosfareContext context) => default;
            }

            public sealed class DefaultHandler : IEventHandler<OrderPlaced>
            {
                public ValueTask HandleAsync(OrderPlaced message, ErgosfareContext context) => default;
            }

            public static class Caller
            {
                private static readonly GroupSet Audit = GroupSet.Of("audit");

                public static ValueTask Publish(IEventMediator mediator)
                    => mediator.PublishAsync(new OrderPlaced(), Audit);
            }
        }
        """;

    /// <summary>
    ///     The set the call site proves becomes a key: the plan for <c>{"audit"}</c> carries
    ///     the grouped handler, and the default plan — which every message gets attempted —
    ///     carries the ungrouped one. Two sets, two pipelines, two plans.
    /// </summary>
    [Fact]
    public void ProvenGroupSet_KeysItsOwnPlan()
    {
        var result = GeneratorTestHost.Run(GroupedPipeline);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);

        // The default set is attempted first, so it takes the lower plan number and keeps
        // the argument-less overload — an absent key means the default group.
        Assert.Contains(
            "AddBroadcastPlan<global::TestApp.OrderPlaced>(new StagedPlan0());",
            result.GeneratedSource);

        // The proven set follows, and travels with its plan as the key.
        Assert.Contains(
            "AddBroadcastPlan<global::TestApp.OrderPlaced>(new StagedPlan1(), new string[] { \"audit\" });",
            result.GeneratedSource);
    }

    /// <summary>
    ///     The documented idiom is a reused <c>static readonly GroupSet</c>, so the reader
    ///     follows a named reference to its initializer — refusing to would leave the
    ///     recommended spelling unprovable and unplanned.
    /// </summary>
    [Fact]
    public void GroupSetHeldInAField_IsStillProven()
    {
        var result = GeneratorTestHost.Run(GroupedPipeline);

        Assert.Contains("new string[] { \"audit\" }", result.GeneratedSource);
    }

    /// <summary>
    ///     A set assembled at run time proves nothing. The message keeps its default plan,
    ///     and the filtered dispatch keeps the runtime group lane — correct either way, and
    ///     the only honest outcome when the filter is not a compile-time fact.
    /// </summary>
    [Fact]
    public void UnprovableGroupSet_KeysNoPlan()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Attributes;
            using Stella.Ergosfare.Events.Abstractions;
            using System.Threading.Tasks;

            namespace TestApp
            {
                public sealed record OrderPlaced : IEvent;

                [Group("audit")]
                public sealed class AuditHandler : IEventHandler<OrderPlaced>
                {
                    public ValueTask HandleAsync(OrderPlaced message, ErgosfareContext context) => default;
                }

                public static class Caller
                {
                    public static ValueTask Publish(IEventMediator mediator, string[] runtimeGroups)
                        => mediator.PublishAsync(new OrderPlaced(), runtimeGroups);
                }
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);
        Assert.DoesNotContain("AddBroadcastPlan<global::TestApp.OrderPlaced>(new StagedPlan0(), new string[]", result.GeneratedSource);
    }

    /// <summary>
    ///     Group selection is an any-of test, so order and repetition select the same
    ///     participants. Two spellings of one set must therefore key one plan — otherwise a
    ///     dispatch spelled the other way round would miss a plan baked for it.
    /// </summary>
    [Fact]
    public void GroupSetSpellings_NormalizeToOneKey()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Attributes;
            using Stella.Ergosfare.Events.Abstractions;
            using System.Threading.Tasks;

            namespace TestApp
            {
                public sealed record OrderPlaced : IEvent;

                [Group("audit")]
                public sealed class AuditHandler : IEventHandler<OrderPlaced>
                {
                    public ValueTask HandleAsync(OrderPlaced message, ErgosfareContext context) => default;
                }

                [Group("billing")]
                public sealed class BillingHandler : IEventHandler<OrderPlaced>
                {
                    public ValueTask HandleAsync(OrderPlaced message, ErgosfareContext context) => default;
                }

                public static class Caller
                {
                    public static ValueTask One(IEventMediator mediator)
                        => mediator.PublishAsync(new OrderPlaced(), GroupSet.Of("audit", "billing"));

                    public static ValueTask Other(IEventMediator mediator)
                        => mediator.PublishAsync(new OrderPlaced(), GroupSet.Of("billing", "audit"));
                }
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);

        // Sorted, deduplicated, keyed once — not once per spelling. (The manifest records
        // the same normalized set for the reachability side, which is why the count is
        // taken over the plan registrations rather than the whole file.)
        var occurrences = CountOccurrences(
            result.GeneratedSource,
            "AddBroadcastPlan<global::TestApp.OrderPlaced>(new StagedPlan0(), new string[] { \"audit\", \"billing\" });");

        Assert.Equal(1, occurrences);
        Assert.DoesNotContain("new string[] { \"billing\", \"audit\" }", result.GeneratedSource);
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;

        for (var index = source.IndexOf(value, StringComparison.Ordinal);
             index >= 0;
             index = source.IndexOf(value, index + value.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
