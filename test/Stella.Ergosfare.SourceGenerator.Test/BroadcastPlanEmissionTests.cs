namespace Stella.Ergosfare.SourceGenerator.Test;

/// <summary>
///     Broadcast plan emission: an event is a resultless pipeline like a void command, with
///     one difference — every matched handler runs instead of a sole one. The plan carries
///     both handler segments, directly registered first and covariantly matched after, and
///     the body delivers to them in that order.
/// </summary>
public class BroadcastPlanEmissionTests
{
    private const string EventPipeline = """
        using Stella.Ergosfare.Core.Abstractions;
        using Stella.Ergosfare.Events.Abstractions;
        using System.Threading.Tasks;

        namespace TestApp
        {
            public sealed record OrderPlaced : IEvent;

            public sealed class AuditHandler : IEventHandler<OrderPlaced>
            {
                public ValueTask HandleAsync(OrderPlaced message, ErgosfareContext context) => default;
            }

            public sealed class NotifyHandler : IEventHandler<OrderPlaced>
            {
                public ValueTask HandleAsync(OrderPlaced message, ErgosfareContext context) => default;
            }

            public sealed class OrderPlacedInterceptor : IEventPreInterceptor<OrderPlaced>
            {
                public ValueTask<OrderPlaced> HandleAsync(OrderPlaced message, ErgosfareContext context)
                    => ValueTask.FromResult(message);
            }
        }
        """;

    /// <summary>
    ///     The plan exists at all — the event lane never had one, so this is the whole point:
    ///     an interceptor-bearing broadcast gets a compiled body instead of the runtime
    ///     delivery.
    /// </summary>
    [Fact]
    public void InterceptedBroadcast_EmitsABroadcastPlan()
    {
        var result = GeneratorTestHost.Run(EventPipeline);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);

        // Its own family: a publish looks in the broadcast store and a send in the resultless
        // one, so which store answered settles the delivery difference — nothing branches on
        // the message.
        Assert.Contains(
            "GeneratedDispatchRoots.AddBroadcastPlan<global::TestApp.OrderPlaced>(new StagedPlan0());",
            result.GeneratedSource);
        Assert.Contains("StagedBroadcastPlan<global::TestApp.OrderPlaced>", result.GeneratedSource);
        Assert.DoesNotContain("StagedVoidPlan<global::TestApp.OrderPlaced>", result.GeneratedSource);
    }

    /// <summary>
    ///     Both handlers are baked into the direct segment, in the runtime's own order, and
    ///     both are called. The indirect segment is empty here — nobody registered against a
    ///     base type.
    /// </summary>
    [Fact]
    public void EveryMatchedHandler_IsBakedAndCalled()
    {
        var result = GeneratorTestHost.Run(EventPipeline);

        Assert.Empty(result.CompilationErrors);

        Assert.Contains(
            "new global::System.Type[] { typeof(global::TestApp.AuditHandler), typeof(global::TestApp.NotifyHandler) }",
            result.GeneratedSource);

        Assert.Contains("GetRequiredService<global::TestApp.AuditHandler>(serviceProvider).HandleAsync(message, context);",
            result.GeneratedSource);
        Assert.Contains("GetRequiredService<global::TestApp.NotifyHandler>(serviceProvider).HandleAsync(message, context);",
            result.GeneratedSource);
    }

    /// <summary>
    ///     A handler registered against a base event lands in the covariant segment, after the
    ///     direct one — the split the runtime makes, and the reason a broadcast needs its own
    ///     handler assembly: for a single-handler pipeline that same handler is a competing
    ///     claim that disqualifies the plan.
    /// </summary>
    [Fact]
    public void CovariantHandler_LandsInTheIndirectSegment()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Events.Abstractions;
            using System.Threading.Tasks;

            namespace TestApp
            {
                public interface IDomainEvent : IEvent;

                public sealed record OrderPlaced : IDomainEvent;

                public sealed class DirectHandler : IEventHandler<OrderPlaced>
                {
                    public ValueTask HandleAsync(OrderPlaced message, ErgosfareContext context) => default;
                }

                public sealed class CatchAllHandler : IEventHandler<IDomainEvent>
                {
                    public ValueTask HandleAsync(IDomainEvent message, ErgosfareContext context) => default;
                }

                public sealed class OrderPlacedInterceptor : IEventPreInterceptor<OrderPlaced>
                {
                    public ValueTask<OrderPlaced> HandleAsync(OrderPlaced message, ErgosfareContext context)
                        => ValueTask.FromResult(message);
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);

        // Direct segment, then the covariant one — two separate arrays in the baked key.
        Assert.Contains(
            "new global::System.Type[] { typeof(global::TestApp.DirectHandler) },",
            result.GeneratedSource);
        Assert.Contains(
            "new global::System.Type[] { typeof(global::TestApp.CatchAllHandler) },",
            result.GeneratedSource);
    }

    /// <summary>
    ///     A command plan is the same shape with one direct handler and an empty covariant
    ///     segment — that is what lets one gate and one emission path serve both families.
    /// </summary>
    [Fact]
    public void SingleHandlerPlan_KeepsTheOneHandlerShape()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions;
            using System.Threading.Tasks;

            namespace TestApp
            {
                public sealed record Ping : ICommand;

                public sealed class PingHandler : ICommandHandler<Ping>
                {
                    public ValueTask HandleAsync(Ping message, ErgosfareContext context) => default;
                }

                public sealed class PingInterceptor : ICommandPreInterceptor<Ping>
                {
                    public ValueTask<Ping> HandleAsync(Ping message, ErgosfareContext context)
                        => ValueTask.FromResult(message);
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);

        Assert.Contains(
            "new global::System.Type[] { typeof(global::TestApp.PingHandler) },",
            result.GeneratedSource);
        Assert.Contains("global::System.Array.Empty<global::System.Type>(),", result.GeneratedSource);
    }

    /// <summary>
    ///     An event whose handlers carry no interceptor and no plugin gets a plan too — its
    ///     bare loop IS the plan. A command in that shape falls back to the single-handler
    ///     plan family; a publish has no such family, so without this the flagship
    ///     "hooks cost zero while not attached" lane would be the one lane still resolving
    ///     its handlers through the container on every dispatch.
    /// </summary>
    [Fact]
    public void PlainBroadcast_GetsABarePlan()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Events.Abstractions;
            using System.Threading.Tasks;

            namespace TestApp
            {
                public sealed record OrderPlaced : IEvent;

                public sealed class AuditHandler : IEventHandler<OrderPlaced>
                {
                    public ValueTask HandleAsync(OrderPlaced message, ErgosfareContext context) => default;
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);
        Assert.Contains(
            "AddBroadcastPlan<global::TestApp.OrderPlaced>(new StagedPlan0());",
            result.GeneratedSource);

        // A publish looks only in the broadcast store, so a single-handler event plan filed
        // under the sending one would be emitted, validated and never run.
        Assert.Contains("StagedBroadcastPlan<global::TestApp.OrderPlaced>", result.GeneratedSource);
        Assert.DoesNotContain("StagedVoidPlan<global::TestApp.OrderPlaced>", result.GeneratedSource);
    }
}
