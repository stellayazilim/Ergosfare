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
    public void SoleStreamHandler_EmitsTheStreamPlan()
    {
        var result = GeneratorTestHost.Run("""
            #pragma warning disable CS0618
            using System.Collections.Generic;
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Queries.Abstractions;

            namespace TestApp
            {
                public sealed record TickStream : IStreamQuery<int>;

                public sealed class TickStreamHandler : IStreamQueryHandler<TickStream, int>
                {
                    public async IAsyncEnumerable<int> StreamAsync(TickStream query, ErgosfareContext context)
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
            "GeneratedDispatchRoots.AddStreamPlan<global::TestApp.TickStream, int>(new StagedPlan0());",
            result.GeneratedSource);
        Assert.Contains(
            "private sealed class StagedPlan0 : global::Stella.Ergosfare.Core.Abstractions.StagedPlans.StagedStreamPlan<global::TestApp.TickStream, int>",
            result.GeneratedSource);

        // The handler is resolved concretely and called through the stream-handler
        // contract, whose Handle is a default interface member.
        Assert.Contains(
            "((global::Stella.Ergosfare.Core.Abstractions.Handlers.IHandler<global::TestApp.TickStream, global::System.Collections.Generic.IAsyncEnumerable<int>>)"
            + "global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<global::TestApp.TickStreamHandler>(serviceProvider)).Handle(message, context);",
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
        var result = GeneratorTestHost.Run("""
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

                public sealed class StagedTickStreamHandler : IStreamQueryHandler<StagedTickStream, int>
                {
                    public async IAsyncEnumerable<int> StreamAsync(StagedTickStream query, ErgosfareContext context)
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
        Assert.Contains("object? postChain = enumerator;", result.GeneratedSource);
        Assert.Contains(
            "IAsyncPostInterceptor<global::TestApp.StagedTickStream, global::System.Collections.Generic.IAsyncEnumerator<int>>)",
            result.GeneratedSource);
        Assert.Contains(").HandleAsync(message, enumerator, exception, context);", result.GeneratedSource);

        // The final stage runs from a finally, skipped when a participant aborted.
        Assert.Contains("if (!aborted)", result.GeneratedSource);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void KeyedStreamHandler_SuppressesTheStreamPlan()
    {
        var result = GeneratorTestHost.Run("""
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
                public sealed class KeyedTickStreamHandler : IStreamQueryHandler<KeyedTickStream, int>
                {
                    public async IAsyncEnumerable<int> StreamAsync(KeyedTickStream query, ErgosfareContext context)
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
        Assert.DoesNotContain("AddStreamPlan<global::TestApp.KeyedTickStream", result.GeneratedSource);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void NestedStreamFixtures_SuppressTheStreamPlan()
    {
        var result = GeneratorTestHost.Run("""
            #pragma warning disable CS0618
            using System.Collections.Generic;
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Queries.Abstractions;

            namespace TestApp
            {
                public static class Holder
                {
                    public sealed record NestedTickStream : IStreamQuery<int>;

                    public sealed class NestedTickStreamHandler : IStreamQueryHandler<NestedTickStream, int>
                    {
                        public async IAsyncEnumerable<int> StreamAsync(NestedTickStream query, ErgosfareContext context)
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
        var result = GeneratorTestHost.Run("""
            #pragma warning disable CS0618
            using System.Collections.Generic;
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Queries.Abstractions;

            namespace TestApp
            {
                public sealed record ContestedTickStream : IStreamQuery<int>;

                public sealed class FirstTickStreamHandler : IStreamQueryHandler<ContestedTickStream, int>
                {
                    public async IAsyncEnumerable<int> StreamAsync(ContestedTickStream query, ErgosfareContext context)
                    {
                        await System.Threading.Tasks.Task.Yield();
                        yield return 1;
                    }
                }

                public sealed class SecondTickStreamHandler : IStreamQueryHandler<ContestedTickStream, int>
                {
                    public async IAsyncEnumerable<int> StreamAsync(ContestedTickStream query, ErgosfareContext context)
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
        var result = GeneratorTestHost.Run("""
            #pragma warning disable CS0618
            using System.Collections.Generic;
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Attributes;
            using Stella.Ergosfare.Queries.Abstractions;

            namespace TestApp
            {
                public sealed record RoutedTickStream : IStreamQuery<int>;

                [Group("east")]
                public sealed class EastTickStreamHandler : IStreamQueryHandler<RoutedTickStream, int>
                {
                    public async IAsyncEnumerable<int> StreamAsync(RoutedTickStream query, ErgosfareContext context)
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
        var result = GeneratorTestHost.Run("""
            #pragma warning disable CS0618
            using System.Collections.Generic;
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Queries.Abstractions;

            namespace TestApp
            {
                public interface IAuditedStream : IStreamQuery<int>;

                public sealed record AuditedTickStream : IAuditedStream;

                public sealed class AuditedTickStreamHandler : IStreamQueryHandler<AuditedTickStream, int>
                {
                    public async IAsyncEnumerable<int> StreamAsync(AuditedTickStream query, ErgosfareContext context)
                    {
                        await System.Threading.Tasks.Task.Yield();
                        yield return 1;
                    }
                }

                public sealed class AuditedBaseStreamHandler : IStreamQueryHandler<IAuditedStream, int>
                {
                    public async IAsyncEnumerable<int> StreamAsync(IAuditedStream query, ErgosfareContext context)
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
