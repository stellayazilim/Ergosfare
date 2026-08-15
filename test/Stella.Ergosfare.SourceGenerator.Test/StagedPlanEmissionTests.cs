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
                    public ValueTask HandleAsync(StagedPing message, Stella.Ergosfare.Core.Abstractions.ErgosfareContext context)
                        => default;
                }

                public sealed class StagedPingInterceptor : ICommandPreInterceptor<StagedPing>
                {
                    public ValueTask<StagedPing> HandleAsync(StagedPing message, Stella.Ergosfare.Core.Abstractions.ErgosfareContext context)
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

    /// <summary>
    ///     A handler on one of the message's base contracts is the recommended cross-cutting
    ///     idiom, and the priority ladder resolves it without hesitation: the direct handler
    ///     serves the message, the covariant one is its fallback. So the message is plannable
    ///     — the plan delivers to the direct handler alone, and carries the covariant one in
    ///     its composition because that is what the gate compares the live pipeline against.
    /// </summary>
    [Fact]
    public void CovariantSibling_KeepsThePlanAndEntersItsComposition()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions;
            using System.Threading.Tasks;

            namespace TestApp
            {
                public interface IAuditedCommand : ICommand;

                public sealed record TransferMoney : ICommand, IAuditedCommand;

                public sealed class TransferMoneyHandler : ICommandHandler<TransferMoney>
                {
                    public ValueTask HandleAsync(TransferMoney message, ErgosfareContext context) => default;
                }

                public sealed class AuditedCommandHandler : ICommandHandler<IAuditedCommand>
                {
                    public ValueTask HandleAsync(IAuditedCommand message, ErgosfareContext context) => default;
                }

                public sealed class TransferMoneyPre : ICommandPreInterceptor<TransferMoney>
                {
                    public ValueTask<TransferMoney> HandleAsync(TransferMoney message, ErgosfareContext context)
                        => ValueTask.FromResult(message);
                }
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);

        Assert.Contains(
            "GeneratedDispatchRoots.AddStagedPlan<global::TestApp.TransferMoney>(new StagedPlan0());",
            result.GeneratedSource);

        // The covariant handler is in the composition the plan is gated on — and in the
        // second segment, the covariant one. Omitting it would have the gate refuse the plan
        // on every pipeline that has a base-contract handler.
        Assert.Contains(
            "new global::System.Type[] { typeof(global::TestApp.TransferMoneyHandler) },\n"
            + "                new global::System.Type[] { typeof(global::TestApp.AuditedCommandHandler) }",
            result.GeneratedSource.Replace("\r\n", "\n"));

        // And it is not delivered to: the direct handler is the only main-handler call in
        // the body, exactly as the ladder prescribes.
        Assert.Contains("typeof(global::TestApp.TransferMoneyHandler)", result.GeneratedSource);
        Assert.DoesNotContain(
            "global::TestApp.AuditedCommandHandler>().HandleAsync(message, context);",
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
                    public ValueTask HandleAsync(OrderedPing message, Stella.Ergosfare.Core.Abstractions.ErgosfareContext context)
                        => default;
                }

                // Direct segment: weight descending, then ordinal type name.
                public sealed class LightInterceptor : ICommandPreInterceptor<OrderedPing>
                {
                    public ValueTask<OrderedPing> HandleAsync(OrderedPing message, Stella.Ergosfare.Core.Abstractions.ErgosfareContext context)
                        => ValueTask.FromResult(message);
                }

                [Weight(5)]
                public sealed class HeavyInterceptor : ICommandPreInterceptor<OrderedPing>
                {
                    public ValueTask<OrderedPing> HandleAsync(OrderedPing message, Stella.Ergosfare.Core.Abstractions.ErgosfareContext context)
                        => ValueTask.FromResult(message);
                }

                // Indirect segment (registered for the marker interface): always after the
                // direct segment regardless of weight.
                [Weight(9)]
                public sealed class BroadInterceptor : ICommandPreInterceptor
                {
                    public ValueTask<object> HandleAsync(ICommand message, Stella.Ergosfare.Core.Abstractions.ErgosfareContext context)
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
                    public ValueTask HandleAsync(GroupedStagedPing message, Stella.Ergosfare.Core.Abstractions.ErgosfareContext context)
                        => default;
                }

                [Group("reporting")]
                public sealed class GroupedStagedPingInterceptor : ICommandPreInterceptor<GroupedStagedPing>
                {
                    public ValueTask<GroupedStagedPing> HandleAsync(GroupedStagedPing message, Stella.Ergosfare.Core.Abstractions.ErgosfareContext context)
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
                    public ValueTask<int> HandleAsync(StagedNumberQuery message, Stella.Ergosfare.Core.Abstractions.ErgosfareContext context)
                        => ValueTask.FromResult(7);
                }

                public sealed class StagedNumberQueryPostInterceptor : IQueryPostInterceptor<StagedNumberQuery, int>
                {
                    public ValueTask<int> HandleAsync(StagedNumberQuery query, int queryResult, Stella.Ergosfare.Core.Abstractions.ErgosfareContext context)
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
    public void ReferenceTypedStages_EachTakeTheCastTheirOwnContractDeclares()
    {
        var result = GeneratorTestHost.Run("""
            using System;
            using System.Threading.Tasks;
            using Stella.Ergosfare.Queries.Abstractions;

            namespace TestApp
            {
                public sealed record StagedTextQuery : IQuery<string>;

                public sealed class StagedTextQueryHandler : IQueryHandler<StagedTextQuery, string>
                {
                    public ValueTask<string> HandleAsync(StagedTextQuery message, Stella.Ergosfare.Core.Abstractions.ErgosfareContext context)
                        => ValueTask.FromResult("ok");
                }

                public sealed class StagedTextQueryPost : IQueryPostInterceptor<StagedTextQuery, string>
                {
                    public ValueTask<string> HandleAsync(StagedTextQuery query, string queryResult, Stella.Ergosfare.Core.Abstractions.ErgosfareContext context)
                        => ValueTask.FromResult(queryResult);
                }

                public sealed class StagedTextQueryException : IQueryExceptionInterceptor<StagedTextQuery, string>
                {
                    public ValueTask<string?> HandleAsync(StagedTextQuery query, string? result, Exception exception, Stella.Ergosfare.Core.Abstractions.ErgosfareContext context)
                        => ValueTask.FromResult(result);
                }

                public sealed class StagedTextQueryFinal : IQueryFinalInterceptor<StagedTextQuery, string>
                {
                    public ValueTask HandleAsync(StagedTextQuery query, string? result, Exception? exception, Stella.Ergosfare.Core.Abstractions.ErgosfareContext context)
                        => default;
                }
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);

        // Every stage used to be cast to TResult?, and a cast to a nullable type resets
        // the expression's nullable state to maybe-null however non-null the chain
        // variable is. The post stage declares `TResult messageResult`, so that produced
        // CS8604 in every consumer's build — an outright failure where warnings are
        // errors. Value-typed results never showed it, which is why it survived so long.
        Assert.DoesNotContain(result.OutputCompilation.GetDiagnostics(), d => d.Id == "CS8604");

        // Post takes TResult; exception and final take TResult?.
        Assert.Contains("(string)postChain, context);", result.GeneratedSource);
        Assert.Contains("(string?)exceptionChain, e, context);", result.GeneratedSource);
        Assert.Contains("(string?)result, exception, context);", result.GeneratedSource);
    }

    [Fact]
    public void ConstructibleParticipants_EmitTheDirectConstructionVariant()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Commands.Abstractions;
            using System.Threading.Tasks;

            namespace TestApp
            {
                public interface IGreeter { }

                public sealed record DirectPing : ICommand;

                public sealed class DirectPingHandler : ICommandHandler<DirectPing>
                {
                    public DirectPingHandler(IGreeter greeter) { }

                    public ValueTask HandleAsync(DirectPing message, Stella.Ergosfare.Core.Abstractions.ErgosfareContext context)
                        => default;
                }

                public sealed class DirectPingInterceptor : ICommandPreInterceptor<DirectPing>
                {
                    public ValueTask<DirectPing> HandleAsync(DirectPing message, Stella.Ergosfare.Core.Abstractions.ErgosfareContext context)
                        => ValueTask.FromResult(message);
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);
        Assert.Contains("public override bool SupportsDirectConstruction", result.GeneratedSource);
        Assert.Contains("ExecuteDirect(", result.GeneratedSource);

        // The direct variant constructs participants with `new` — the parameterless
        // interceptor directly, the dependency-injected handler with its dependencies
        // still resolved from the dispatching provider.
        Assert.Contains("new global::TestApp.DirectPingInterceptor()", result.GeneratedSource);
        Assert.Contains(
            "new global::TestApp.DirectPingHandler(global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<global::TestApp.IGreeter>(serviceProvider))",
            result.GeneratedSource);
    }

    [Fact]
    public void MultiConstructorParticipant_SkipsTheDirectVariantAndReportsTheInfo()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Commands.Abstractions;
            using System.Threading.Tasks;

            namespace TestApp
            {
                public sealed record PickyDirectPing : ICommand;

                public sealed class PickyDirectPingHandler : ICommandHandler<PickyDirectPing>
                {
                    public PickyDirectPingHandler() { }

                    public PickyDirectPingHandler(string dependency) { }

                    public ValueTask HandleAsync(PickyDirectPing message, Stella.Ergosfare.Core.Abstractions.ErgosfareContext context)
                        => default;
                }

                public sealed class PickyDirectPingInterceptor : ICommandPreInterceptor<PickyDirectPing>
                {
                    public ValueTask<PickyDirectPing> HandleAsync(PickyDirectPing message, Stella.Ergosfare.Core.Abstractions.ErgosfareContext context)
                        => ValueTask.FromResult(message);
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);

        // The staged plan itself still emits — only the direct variant is withheld,
        // and the ERGO003 info points at the reason.
        Assert.Contains("AddStagedPlan<global::TestApp.PickyDirectPing>", result.GeneratedSource);
        Assert.DoesNotContain("ExecuteDirect(", result.GeneratedSource);
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "ERGO003");
    }

    [Fact]
    public void FromServicesOnConstructor_ReportsTheInfo()
    {
        var result = GeneratorTestHost.Run("""
            using System;
            using Stella.Ergosfare.Commands.Abstractions;
            using System.Threading.Tasks;

            namespace TestApp
            {
                [AttributeUsage(AttributeTargets.Parameter)]
                public sealed class FromServicesAttribute : Attribute;

                public interface IGreeter { }

                public sealed record AnnotatedPing : ICommand;

                public sealed class AnnotatedPingHandler : ICommandHandler<AnnotatedPing>
                {
                    public AnnotatedPingHandler([FromServices] IGreeter greeter) { }

                    public ValueTask HandleAsync(AnnotatedPing message, Stella.Ergosfare.Core.Abstractions.ErgosfareContext context)
                        => default;
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "ERGO004");
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
                    public ValueTask HandleAsync(ShieldedPing message, Stella.Ergosfare.Core.Abstractions.ErgosfareContext context)
                        => default;
                }

                public sealed class ShieldedPingInterceptor : ICommandPreInterceptor<ShieldedPing>
                {
                    public ValueTask<ShieldedPing> HandleAsync(ShieldedPing message, Stella.Ergosfare.Core.Abstractions.ErgosfareContext context)
                        => ValueTask.FromResult(message);
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);
        Assert.DoesNotContain("AddStagedPlan<global::TestApp.ShieldedPing", result.GeneratedSource);
    }
}
