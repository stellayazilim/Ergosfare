namespace Stella.Ergosfare.SourceGenerator.Test;

/// <summary>
/// Staged pipeline plan emission: an interceptor-bearing message whose whole pipeline is
/// modelable gets a bespoke sealed plan class plus an <c>AddStagedPlan</c> root; anything
/// unmodelable — grouped participants, <c>[ExcludeFromPipeline]</c> messages — keeps the
/// message off the staged store, where the runtime strategy serves it as before.
/// </summary>
public class StagedPlanEmissionTests
{
    [Fact]
    public void InterceptedSoloHandler_EmitsTheStagedPlan()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Commands.Abstractions;
            using System.Threading.Tasks;

            namespace TestApp
            {
                public sealed record StagedPing : ICommand;

                public sealed class StagedPingHandler : ICommandHandler<StagedPing>
                {
                    public ValueTask HandleAsync(StagedPing message, Stella.Ergosfare.Core.Abstractions.IExecutionContext context)
                        => default;
                }

                public sealed class StagedPingInterceptor : ICommandPreInterceptor<StagedPing>
                {
                    public ValueTask<StagedPing> HandleAsync(StagedPing message, Stella.Ergosfare.Core.Abstractions.IExecutionContext context)
                        => ValueTask.FromResult(message);
                }
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);

        // The interceptor suppresses the single-handler plan and produces the staged one.
        Assert.DoesNotContain("AddVoidPlan<global::TestApp.StagedPing", result.GeneratedSource);
        Assert.Contains(
            "GeneratedDispatchRoots.AddStagedPlan<global::TestApp.StagedPing>(new StagedPlan0());",
            result.GeneratedSource);
        Assert.Contains("typeof(global::TestApp.StagedPingHandler)", result.GeneratedSource);
        Assert.Contains(
            "message = (global::TestApp.StagedPing) await ((global::Stella.Ergosfare.Core.Abstractions.Handlers.IAsyncPreInterceptor<global::TestApp.StagedPing>)",
            result.GeneratedSource);
    }

    [Fact]
    public void WeightAndSegmentOrdering_BakesTheRuntimeOrder()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Attributes;
            using System.Threading.Tasks;

            namespace TestApp
            {
                public sealed record OrderedPing : ICommand;

                public sealed class OrderedPingHandler : ICommandHandler<OrderedPing>
                {
                    public ValueTask HandleAsync(OrderedPing message, Stella.Ergosfare.Core.Abstractions.IExecutionContext context)
                        => default;
                }

                // Direct segment: weight descending, then ordinal type name.
                public sealed class LightInterceptor : ICommandPreInterceptor<OrderedPing>
                {
                    public ValueTask<OrderedPing> HandleAsync(OrderedPing message, Stella.Ergosfare.Core.Abstractions.IExecutionContext context)
                        => ValueTask.FromResult(message);
                }

                [Weight(5)]
                public sealed class HeavyInterceptor : ICommandPreInterceptor<OrderedPing>
                {
                    public ValueTask<OrderedPing> HandleAsync(OrderedPing message, Stella.Ergosfare.Core.Abstractions.IExecutionContext context)
                        => ValueTask.FromResult(message);
                }

                // Indirect segment (registered for the marker interface): always after the
                // direct segment regardless of weight.
                [Weight(9)]
                public sealed class BroadInterceptor : ICommandPreInterceptor
                {
                    public ValueTask<object> HandleAsync(ICommand message, Stella.Ergosfare.Core.Abstractions.IExecutionContext context)
                        => ValueTask.FromResult<object>(message);
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);
        Assert.Contains(
            "new global::System.Type[] { typeof(global::TestApp.HeavyInterceptor), typeof(global::TestApp.LightInterceptor), typeof(global::TestApp.BroadInterceptor) }",
            result.GeneratedSource);
    }

    [Fact]
    public void GroupedInterceptor_SuppressesTheStagedPlan()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Attributes;
            using System.Threading.Tasks;

            namespace TestApp
            {
                public sealed record GroupedStagedPing : ICommand;

                public sealed class GroupedStagedPingHandler : ICommandHandler<GroupedStagedPing>
                {
                    public ValueTask HandleAsync(GroupedStagedPing message, Stella.Ergosfare.Core.Abstractions.IExecutionContext context)
                        => default;
                }

                [Group("reporting")]
                public sealed class GroupedStagedPingInterceptor : ICommandPreInterceptor<GroupedStagedPing>
                {
                    public ValueTask<GroupedStagedPing> HandleAsync(GroupedStagedPing message, Stella.Ergosfare.Core.Abstractions.IExecutionContext context)
                        => ValueTask.FromResult(message);
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);

        // A grouped participant makes the group-less pipeline content unmodelable-enough:
        // conservatively no staged plan (and the interceptor also suppresses the
        // single-handler plan) — the runtime strategy serves the message.
        Assert.DoesNotContain("AddStagedPlan<global::TestApp.GroupedStagedPing", result.GeneratedSource);
    }

    [Fact]
    public void ResultPipeline_EmitsTheStagedResultPlan()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Queries.Abstractions;
            using System.Threading.Tasks;

            namespace TestApp
            {
                public sealed record StagedNumberQuery : IQuery<int>;

                public sealed class StagedNumberQueryHandler : IQueryHandler<StagedNumberQuery, int>
                {
                    public ValueTask<int> HandleAsync(StagedNumberQuery message, Stella.Ergosfare.Core.Abstractions.IExecutionContext context)
                        => ValueTask.FromResult(7);
                }

                public sealed class StagedNumberQueryPostInterceptor : IQueryPostInterceptor<StagedNumberQuery, int>
                {
                    public ValueTask<int> HandleAsync(StagedNumberQuery query, int queryResult, Stella.Ergosfare.Core.Abstractions.IExecutionContext context)
                        => ValueTask.FromResult(queryResult);
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);
        Assert.Contains(
            "GeneratedDispatchRoots.AddStagedPlan<global::TestApp.StagedNumberQuery, int>(new StagedPlan0());",
            result.GeneratedSource);

        // The typed async post arm with the value-typed result's unbox-parity casts.
        Assert.Contains(
            "postChain = await ((global::Stella.Ergosfare.Core.Abstractions.Handlers.IAsyncPostInterceptor<global::TestApp.StagedNumberQuery, int>)",
            result.GeneratedSource);
        Assert.Contains("result = (int) postChain!;", result.GeneratedSource);
        Assert.Contains("return result;", result.GeneratedSource);
    }

    [Fact]
    public void ExcludeFromPipelineMessage_SuppressesTheStagedPlan()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Attributes;
            using System.Threading.Tasks;

            namespace TestApp
            {
                [ExcludeFromPipeline]
                public sealed record ShieldedPing : ICommand;

                public sealed class ShieldedPingHandler : ICommandHandler<ShieldedPing>
                {
                    public ValueTask HandleAsync(ShieldedPing message, Stella.Ergosfare.Core.Abstractions.IExecutionContext context)
                        => default;
                }

                public sealed class ShieldedPingInterceptor : ICommandPreInterceptor<ShieldedPing>
                {
                    public ValueTask<ShieldedPing> HandleAsync(ShieldedPing message, Stella.Ergosfare.Core.Abstractions.IExecutionContext context)
                        => ValueTask.FromResult(message);
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);
        Assert.DoesNotContain("AddStagedPlan<global::TestApp.ShieldedPing", result.GeneratedSource);
    }
}
