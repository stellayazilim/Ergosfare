namespace Stella.Ergosfare.SourceGenerator.Test;

/// <summary>
/// Stream plan emission: a streaming query whose (query, item) pair is modelable gets a
/// bespoke <c>StagedStreamPlan</c> class plus an <c>AddStreamPlan</c> root; anything
/// unmodelable — keyed, nested or grouped participants, a contested or covariant claim —
/// keeps the pair off the stream store, where the engine fails the dispatch loudly.
/// </summary>
public class StreamPlanEmissionTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void EnumerableResult_EmitsStreamPlanWithoutStreamContracts()
    {
        var result = GeneratorTestHost.RunWithAllCandidates("""
            #pragma warning disable CS0618
            using System.Collections.Generic;
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Queries.Abstractions;

            namespace TestApp
            {
                public sealed record TickStream : IQuery<IAsyncEnumerable<int>>;

                public sealed class TickStreamHandler : IQueryHandler<TickStream, IAsyncEnumerable<int>>
                {
                    public global::System.Threading.Tasks.ValueTask<IAsyncEnumerable<int>> HandleAsync(TickStream query, ErgosfareContext context)
                        => new(Enumerate(query, context));

                    private async IAsyncEnumerable<int> Enumerate(TickStream query, ErgosfareContext context)
                    {
                        await System.Threading.Tasks.Task.Yield();
                        yield return 1;
                    }
                }
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);

        // The pair gets its plan even with no interceptor at all: streaming has no
        // single-handler family to fall back on, so the bare enumeration is the plan.
        Assert.Contains(
            "GeneratedPlanRegistry.AddStreamPlan<global::TestApp.TickStream, int>(new StagedPlan0());",
            result.GeneratedSource);
        Assert.Contains(
            "private sealed class StagedPlan0 : global::Stella.Ergosfare.Core.Abstractions.StagedPlans.StagedStreamPlan<global::TestApp.TickStream, int>",
            result.GeneratedSource);

        // The handler is resolved concretely and called through the stream-handler
        // contract, whose Handle is a default interface member.
        Assert.Contains(
            "((global::Stella.Ergosfare.Core.Abstractions.Handlers.IAsyncHandler<global::TestApp.TickStream, global::System.Collections.Generic.IAsyncEnumerable<int>>)"
            + "new global::TestApp.TickStreamHandler()).HandleAsync(message, context);",
            result.GeneratedSource);
        Assert.Contains("yield return item;", result.GeneratedSource);

        // No exception interceptor: a deferred failure is rethrown with its original stack.
        Assert.Contains(
            "global::System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception).Throw();",
            result.GeneratedSource);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void InterceptedStream_ClosesItsStagesOverTheEnumerator()
    {
        var result = GeneratorTestHost.RunWithAllCandidates("""
            #pragma warning disable CS0618
            using System;
            using System.Collections.Generic;
            using System.Threading.Tasks;
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Handlers;
            using Stella.Ergosfare.Queries.Abstractions;

            namespace TestApp
            {
                public sealed record StagedTickStream : IStreamQuery<int>;

                public sealed class StagedTickStreamHandler : IQueryHandler<StagedTickStream, IAsyncEnumerable<int>>
                {
                    public global::System.Threading.Tasks.ValueTask<IAsyncEnumerable<int>> HandleAsync(StagedTickStream query, ErgosfareContext context)
                        => new(Enumerate(query, context));

                    private async IAsyncEnumerable<int> Enumerate(StagedTickStream query, ErgosfareContext context)
                    {
                        await Task.Yield();
                        yield return 1;
                    }
                }

                public sealed class StagedTickStreamPre : IQueryPreInterceptor<StagedTickStream>
                {
                    public ValueTask<StagedTickStream> HandleAsync(StagedTickStream query, ErgosfareContext context)
                        => ValueTask.FromResult(query);
                }

                // The typed stream-stage contracts have no flavored aliases — the flavored
                // typed shapes constrain TQuery to IQuery<TResult> — so a stream's typed
                // interceptor carries the module marker itself.
                public sealed class StagedTickStreamPost : IQuery, IAsyncPostInterceptor<StagedTickStream, IAsyncEnumerator<int>>
                {
                    public ValueTask<object> HandleAsync(StagedTickStream query, IAsyncEnumerator<int> messageResult, ErgosfareContext context)
                        => ValueTask.FromResult<object>(messageResult);
                }

                public sealed class StagedTickStreamFinal : IQuery, IAsyncFinalInterceptor<StagedTickStream>
                {
                    public ValueTask HandleAsync(StagedTickStream query, object? messageResult, Exception? exception, ErgosfareContext context)
                        => default;
                }
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);

        Assert.Contains(
            "AddStreamPlan<global::TestApp.StagedTickStream, int>(new StagedPlan0());",
            result.GeneratedSource);

        // The pre stage rewrites the message before the handler is asked for the stream.
        Assert.Contains(
            "message = (global::TestApp.StagedTickStream) await ((global::Stella.Ergosfare.Core.Abstractions.Handlers.IAsyncPreInterceptor<global::TestApp.StagedTickStream>)",
            result.GeneratedSource);

        // The post stage closes over the enumerator — the items are already with the
        // caller — and the final stage receives it with the pending exception.
        Assert.DoesNotContain("object? postChain = enumerator;", result.GeneratedSource);
        Assert.DoesNotContain(
            "IAsyncPostInterceptor<global::TestApp.StagedTickStream, global::System.Collections.Generic.IAsyncEnumerator<int>>)",
            result.GeneratedSource);
        Assert.Contains(").HandleAsync(message, enumerator, exception, context);", result.GeneratedSource);

        // The final stage runs from a finally, skipped when a participant aborted.
        Assert.Contains("if (exception is not global::Stella.Ergosfare.Core.Abstractions.Exceptions.ExecutionAbortedException", result.GeneratedSource);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void SelectedKeyedStreamHandler_HasAStreamPlan()
    {
        var result = GeneratorTestHost.RunWithAllCandidates("""
            #pragma warning disable CS0618
            using System.Collections.Generic;
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Attributes;
            using Stella.Ergosfare.Queries.Abstractions;

            namespace TestApp
            {
                [DiscoveryKey("area")]
                public sealed record KeyedTickStream : IStreamQuery<int>;

                [DiscoveryKey("area")]
                public sealed class KeyedTickStreamHandler : IQueryHandler<KeyedTickStream, IAsyncEnumerable<int>>
                {
                    public global::System.Threading.Tasks.ValueTask<IAsyncEnumerable<int>> HandleAsync(KeyedTickStream query, ErgosfareContext context)
                        => new(Enumerate(query, context));

                    private async IAsyncEnumerable<int> Enumerate(KeyedTickStream query, ErgosfareContext context)
                    {
                        await System.Threading.Tasks.Task.Yield();
                        yield return 1;
                    }
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);

        // A keyed handler may not be registered at all; the pair stays off the store and
        // the engine fails its dispatches loudly.
        Assert.Contains("AddStreamPlan<global::TestApp.KeyedTickStream", result.GeneratedSource);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void NestedStreamFixtures_SuppressTheStreamPlan()
    {
        var result = GeneratorTestHost.RunWithAllCandidates("""
            #pragma warning disable CS0618
            using System.Collections.Generic;
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Queries.Abstractions;

            namespace TestApp
            {
                public static class Holder
                {
                    public sealed record NestedTickStream : IStreamQuery<int>;

                    public sealed class NestedTickStreamHandler : IQueryHandler<NestedTickStream, IAsyncEnumerable<int>>
                    {
                        public global::System.Threading.Tasks.ValueTask<IAsyncEnumerable<int>> HandleAsync(NestedTickStream query, ErgosfareContext context)
                            => new(Enumerate(query, context));

                        private async IAsyncEnumerable<int> Enumerate(NestedTickStream query, ErgosfareContext context)
                        {
                            await System.Threading.Tasks.Task.Yield();
                            yield return 1;
                        }
                    }
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);
        Assert.DoesNotContain("AddStreamPlan<", result.GeneratedSource);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ContestedStreamPair_SuppressesTheStreamPlan()
    {
        var result = GeneratorTestHost.RunWithAllCandidates("""
            #pragma warning disable CS0618
            using System.Collections.Generic;
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Queries.Abstractions;

            namespace TestApp
            {
                public sealed record ContestedTickStream : IStreamQuery<int>;

                public sealed class FirstTickStreamHandler : IQueryHandler<ContestedTickStream, IAsyncEnumerable<int>>
                {
                    public global::System.Threading.Tasks.ValueTask<IAsyncEnumerable<int>> HandleAsync(ContestedTickStream query, ErgosfareContext context)
                        => new(Enumerate(query, context));

                    private async IAsyncEnumerable<int> Enumerate(ContestedTickStream query, ErgosfareContext context)
                    {
                        await System.Threading.Tasks.Task.Yield();
                        yield return 1;
                    }
                }

                public sealed class SecondTickStreamHandler : IQueryHandler<ContestedTickStream, IAsyncEnumerable<int>>
                {
                    public global::System.Threading.Tasks.ValueTask<IAsyncEnumerable<int>> HandleAsync(ContestedTickStream query, ErgosfareContext context)
                        => new(Enumerate(query, context));

                    private async IAsyncEnumerable<int> Enumerate(ContestedTickStream query, ErgosfareContext context)
                    {
                        await System.Threading.Tasks.Task.Yield();
                        yield return 2;
                    }
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);

        // A second claim means the plan cannot say which handler serves the dispatch; the
        // engine raises the contest itself.
        Assert.DoesNotContain("AddStreamPlan<global::TestApp.ContestedTickStream", result.GeneratedSource);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GroupedStreamHandler_SuppressesTheStreamPlan()
    {
        var result = GeneratorTestHost.RunWithAllCandidates("""
            #pragma warning disable CS0618
            using System.Collections.Generic;
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Attributes;
            using Stella.Ergosfare.Queries.Abstractions;

            namespace TestApp
            {
                public sealed record RoutedTickStream : IStreamQuery<int>;

                [Group("east")]
                public sealed class EastTickStreamHandler : IQueryHandler<RoutedTickStream, IAsyncEnumerable<int>>
                {
                    public global::System.Threading.Tasks.ValueTask<IAsyncEnumerable<int>> HandleAsync(RoutedTickStream query, ErgosfareContext context)
                        => new(Enumerate(query, context));

                    private async IAsyncEnumerable<int> Enumerate(RoutedTickStream query, ErgosfareContext context)
                    {
                        await System.Threading.Tasks.Task.Yield();
                        yield return 1;
                    }
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);

        // The default set selects no handler, and per-set stream plans do not exist yet.
        Assert.DoesNotContain("AddStreamPlan<global::TestApp.RoutedTickStream", result.GeneratedSource);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void CovariantClaim_SuppressesTheStreamPlan()
    {
        var result = GeneratorTestHost.RunWithAllCandidates("""
            #pragma warning disable CS0618
            using System.Collections.Generic;
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Queries.Abstractions;

            namespace TestApp
            {
                public interface IAuditedStream : IStreamQuery<int>;

                public sealed record AuditedTickStream : IAuditedStream;

                public sealed class AuditedTickStreamHandler : IQueryHandler<AuditedTickStream, IAsyncEnumerable<int>>
                {
                    public global::System.Threading.Tasks.ValueTask<IAsyncEnumerable<int>> HandleAsync(AuditedTickStream query, ErgosfareContext context)
                        => new(Enumerate(query, context));

                    private async IAsyncEnumerable<int> Enumerate(AuditedTickStream query, ErgosfareContext context)
                    {
                        await System.Threading.Tasks.Task.Yield();
                        yield return 1;
                    }
                }

                public sealed class AuditedBaseStreamHandler : IQueryHandler<IAuditedStream, IAsyncEnumerable<int>>
                {
                    public global::System.Threading.Tasks.ValueTask<IAsyncEnumerable<int>> HandleAsync(IAuditedStream query, ErgosfareContext context)
                        => new(Enumerate(query, context));

                    private async IAsyncEnumerable<int> Enumerate(IAuditedStream query, ErgosfareContext context)
                    {
                        await System.Threading.Tasks.Task.Yield();
                        yield return 2;
                    }
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);

        // The strategy's covariant fallback is not compiled this round, and baking the
        // direct handler over a live pipeline that also holds the covariant claim would
        // fail the composition check on every stream.
        Assert.DoesNotContain("AddStreamPlan<global::TestApp.AuditedTickStream", result.GeneratedSource);
    }
}
