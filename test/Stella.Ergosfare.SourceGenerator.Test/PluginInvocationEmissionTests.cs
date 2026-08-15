namespace Stella.Ergosfare.SourceGenerator.Test;

/// <summary>
///     Plugin call emission: a <c>[PipelineInvokable]</c> method becomes a straight call baked
///     into the plan body at the hook it declared, closed over the plan's own message type. The
///     filters — family, discovery key, generic constraint — are decided at emission, so a plan
///     they exclude carries no call and no runtime check at all.
/// </summary>
/// <remarks>
///     Every test asserts <c>CompilationErrors</c> is empty. That is not ceremony here: the
///     emitted call is closed over concrete types in the consumer's compilation, so a filter
///     that admits a plan it should not is a broken consumer build, and this is where that
///     shows up.
/// </remarks>
public class PluginInvocationEmissionTests
{
    /// <summary>A void command with one pre interceptor, plus an observer on the handler seam.</summary>
    [Fact]
    public void Invokable_IsCalledInTheStagedPlanBody()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Plugins.Abstractions;
            using System.Threading.Tasks;

            namespace TestApp
            {
                public sealed record PluggedPing : ICommand;

                public sealed class PluggedPingHandler : ICommandHandler<PluggedPing>
                {
                    public ValueTask HandleAsync(PluggedPing message, ErgosfareContext context) => default;
                }

                public sealed class PluggedPingInterceptor : ICommandPreInterceptor<PluggedPing>
                {
                    public ValueTask<PluggedPing> HandleAsync(PluggedPing message, ErgosfareContext context)
                        => ValueTask.FromResult(message);
                }

                public sealed class CountingHooks
                {
                    [PipelineInvokable(Hook.PostMain)]
                    public void Count<TMessage>(TMessage message, ErgosfareContext context)
                    {
                    }
                }
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);

        // Closed over the plan's concrete message type — no object parameter, no boxing —
        // and resolved from the dispatching provider like every other participant.
        Assert.Contains(
            "GetRequiredService<global::TestApp.CountingHooks>(serviceProvider)"
            + ".Count<global::TestApp.PluggedPing>(message, context);",
            result.GeneratedSource);

        // A void method is a plain call: no await, so it never enters the state machine.
        Assert.DoesNotContain("await global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions"
                              + ".GetRequiredService<global::TestApp.CountingHooks>", result.GeneratedSource);
    }

    /// <summary>
    ///     One declaration serves every family. No hook carries a result, so the same method —
    ///     generic over the message alone — is emitted into a resultless command's plan and a
    ///     result-producing query's plan without the author writing either shape twice.
    /// </summary>
    [Fact]
    public void OneDeclaration_ReachesBothTheVoidAndTheResultPlan()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Plugins.Abstractions;
            using Stella.Ergosfare.Queries.Abstractions;
            using System.Threading.Tasks;

            namespace TestApp
            {
                public sealed record WritePing : ICommand;

                public sealed class WritePingHandler : ICommandHandler<WritePing>
                {
                    public ValueTask HandleAsync(WritePing message, ErgosfareContext context) => default;
                }

                public sealed record CountQuery : IQuery<int>;

                public sealed class CountQueryHandler : IQueryHandler<CountQuery, int>
                {
                    public ValueTask<int> HandleAsync(CountQuery message, ErgosfareContext context)
                        => ValueTask.FromResult(1);
                }

                public sealed class TracingHooks
                {
                    [PipelineInvokable(Hook.PostMain)]
                    public ValueTask ObserveAsync<TMessage>(TMessage message, ErgosfareContext context)
                        => default;
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);

        // The result type never reaches the hook's signature — a query's plan closes the call
        // exactly as a command's does.
        Assert.Contains(".ObserveAsync<global::TestApp.WritePing>(message, context);", result.GeneratedSource);
        Assert.Contains(".ObserveAsync<global::TestApp.CountQuery>(message, context);", result.GeneratedSource);
        Assert.DoesNotContain("ObserveAsync<global::TestApp.CountQuery, int>", result.GeneratedSource);

        // A ValueTask-returning method is awaited.
        Assert.Contains("await global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions"
                        + ".GetRequiredService<global::TestApp.TracingHooks>", result.GeneratedSource);
    }

    /// <summary>
    ///     An interceptorless pipeline is served by the single-handler plan today. A plugin
    ///     pulls it into the staged family, because a plan body is the only place a call can
    ///     live — and the handler plan is dropped so the message keeps exactly one plan.
    /// </summary>
    [Fact]
    public void InterceptorlessPipeline_IsPulledIntoTheStagedFamily()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Plugins.Abstractions;
            using System.Threading.Tasks;

            namespace TestApp
            {
                public sealed record BarePing : ICommand;

                public sealed class BarePingHandler : ICommandHandler<BarePing>
                {
                    public ValueTask HandleAsync(BarePing message, ErgosfareContext context) => default;
                }

                public sealed class CountingHooks
                {
                    [PipelineInvokable(Hook.PreMain)]
                    public void Count<TMessage>(TMessage message, ErgosfareContext context)
                    {
                    }
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);

        Assert.Contains(
            "GeneratedDispatchRoots.AddStagedPlan<global::TestApp.BarePing>(new StagedPlan0());",
            result.GeneratedSource);
        Assert.DoesNotContain("AddVoidPlan<global::TestApp.BarePing", result.GeneratedSource);
    }

    /// <summary>
    ///     A plugin cannot change the shape of the pipeline it observes. Every hook is a
    ///     straight-line position, so an interceptorless plan carrying calls at all four of
    ///     them stays the straight line it is without one: no <c>try</c>, no <c>catch</c>, no
    ///     <c>finally</c>, and no abort flag to track.
    /// </summary>
    [Fact]
    public void EveryHook_LeavesACollapsedPlanUnguarded()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Plugins.Abstractions;
            using System.Threading.Tasks;

            namespace TestApp
            {
                public sealed record ClosingPing : ICommand;

                public sealed class ClosingPingHandler : ICommandHandler<ClosingPing>
                {
                    public ValueTask HandleAsync(ClosingPing message, ErgosfareContext context) => default;
                }

                public sealed class ScopeHooks
                {
                    [PipelineInvokable(Hook.Start)]
                    public void Open<TMessage>(TMessage message, ErgosfareContext context)
                    {
                    }

                    [PipelineInvokable(Hook.PreMain)]
                    public void Entering<TMessage>(TMessage message, ErgosfareContext context)
                    {
                    }

                    [PipelineInvokable(Hook.PostMain)]
                    public void Left<TMessage>(TMessage message, ErgosfareContext context)
                    {
                    }

                    [PipelineInvokable(Hook.Finish)]
                    public void Close<TMessage>(TMessage message, ErgosfareContext context)
                    {
                    }
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);

        Assert.Contains(".Open<global::TestApp.ClosingPing>(message, context);", result.GeneratedSource);
        Assert.Contains(".Close<global::TestApp.ClosingPing>(message, context);", result.GeneratedSource);

        Assert.DoesNotContain("catch (", result.GeneratedSource);
        Assert.DoesNotContain("finally", result.GeneratedSource);
        Assert.DoesNotContain("aborted", result.GeneratedSource);
    }

    /// <summary>
    ///     <c>Finish</c> is the end of a pipeline that completed, so it is emitted on the
    ///     success path — after the post chain, inside the pipeline's own <c>try</c> — and
    ///     nowhere else. A failure leaves through the exception path and never reaches it.
    /// </summary>
    [Fact]
    public void FinishHook_SitsOnTheSuccessPathAfterThePostChain()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Plugins.Abstractions;
            using System;
            using System.Threading.Tasks;

            namespace TestApp
            {
                public sealed record FailingPing : ICommand;

                public sealed class FailingPingHandler : ICommandHandler<FailingPing>
                {
                    public ValueTask HandleAsync(FailingPing message, ErgosfareContext context) => default;
                }

                public sealed class FailingPingPost : ICommandPostInterceptor<FailingPing>
                {
                    public ValueTask<object> HandleAsync(FailingPing message, object result, ErgosfareContext context)
                        => ValueTask.FromResult<object>(Unit.Value);
                }

                public sealed class FailingPingExceptionInterceptor : ICommandExceptionInterceptor<FailingPing>
                {
                    public ValueTask<object> HandleAsync(
                        FailingPing message, object? result, Exception exception, ErgosfareContext context)
                        => ValueTask.FromResult<object>(Unit.Value);
                }

                public sealed class MetricHooks
                {
                    [PipelineInvokable(Hook.Finish)]
                    public void Ended<TMessage>(TMessage message, ErgosfareContext context)
                    {
                    }
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);

        var source = result.GeneratedSource;
        const string observed = ".Ended<global::TestApp.FailingPing>";
        const string caught = "catch (global::System.Exception e)";

        // The plan emits a resolving and a direct-construction body, so every position is
        // asserted in both: each call sits after that body's post chain and before its catch.
        Assert.True(source.IndexOf("var invokedPostResult", StringComparison.Ordinal)
                    < source.IndexOf(observed, StringComparison.Ordinal));
        Assert.True(source.IndexOf(observed, StringComparison.Ordinal)
                    < source.IndexOf(caught, StringComparison.Ordinal));
        Assert.True(source.LastIndexOf("var invokedPostResult", StringComparison.Ordinal)
                    < source.LastIndexOf(observed, StringComparison.Ordinal));
        Assert.True(source.LastIndexOf(observed, StringComparison.Ordinal)
                    < source.LastIndexOf(caught, StringComparison.Ordinal));
    }

    /// <summary>The family filter: a command-only plugin never reaches a query plan.</summary>
    [Fact]
    public void ModuleFilter_KeepsThePluginOutOfTheOtherFamily()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Plugins.Abstractions;
            using Stella.Ergosfare.Queries.Abstractions;
            using System.Threading.Tasks;

            namespace TestApp
            {
                public sealed record WritePing : ICommand;

                public sealed class WritePingHandler : ICommandHandler<WritePing>
                {
                    public ValueTask HandleAsync(WritePing message, ErgosfareContext context) => default;
                }

                public sealed record ReadPing : IQuery<int>;

                public sealed class ReadPingHandler : IQueryHandler<ReadPing, int>
                {
                    public ValueTask<int> HandleAsync(ReadPing message, ErgosfareContext context)
                        => ValueTask.FromResult(1);
                }

                [PluginServiceFilter(Module.Command)]
                public sealed class UnitOfWorkHooks
                {
                    [PipelineInvokable(Hook.PostMain)]
                    public void Commit<TMessage>(TMessage message, ErgosfareContext context)
                    {
                    }
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);

        Assert.Contains("Commit<global::TestApp.WritePing>", result.GeneratedSource);
        Assert.DoesNotContain("Commit<global::TestApp.ReadPing", result.GeneratedSource);
        Assert.DoesNotContain("AddStagedPlan<global::TestApp.ReadPing", result.GeneratedSource);
    }

    /// <summary>
    ///     The key filter's default: saying nothing selects the default key alone, the same
    ///     set a pattern-less <c>RegisterGenerated()</c> selects. A keyed message was opted out
    ///     of default discovery by its author, and a silent plugin does not opt it back in.
    /// </summary>
    [Fact]
    public void UnwrittenKeyFilter_SelectsTheDefaultKeyAlone()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Attributes;
            using Stella.Ergosfare.Plugins.Abstractions;
            using System.Threading.Tasks;

            namespace TestApp
            {
                public sealed record DefaultPing : ICommand;

                public sealed class DefaultPingHandler : ICommandHandler<DefaultPing>
                {
                    public ValueTask HandleAsync(DefaultPing message, ErgosfareContext context) => default;
                }

                [DiscoveryKey("reporting")]
                public sealed record KeyedPing : ICommand;

                public sealed class KeyedPingHandler : ICommandHandler<KeyedPing>
                {
                    public ValueTask HandleAsync(KeyedPing message, ErgosfareContext context) => default;
                }

                public sealed class CountingHooks
                {
                    [PipelineInvokable(Hook.PostMain)]
                    public void Count<TMessage>(TMessage message, ErgosfareContext context)
                    {
                    }
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);

        Assert.Contains("Count<global::TestApp.DefaultPing>", result.GeneratedSource);
        Assert.DoesNotContain("Count<global::TestApp.KeyedPing>", result.GeneratedSource);
    }

    /// <summary>Naming the key opts in; the default-key plan is then the one left out.</summary>
    [Fact]
    public void NamedKeyFilter_SelectsThatKeyAlone()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Attributes;
            using Stella.Ergosfare.Plugins.Abstractions;
            using System.Threading.Tasks;

            namespace TestApp
            {
                public sealed record DefaultPing : ICommand;

                public sealed class DefaultPingHandler : ICommandHandler<DefaultPing>
                {
                    public ValueTask HandleAsync(DefaultPing message, ErgosfareContext context) => default;
                }

                [DiscoveryKey("reporting")]
                public sealed record KeyedPing : ICommand;

                public sealed class KeyedPingHandler : ICommandHandler<KeyedPing>
                {
                    public ValueTask HandleAsync(KeyedPing message, ErgosfareContext context) => default;
                }

                [PluginServiceFilter("reporting")]
                public sealed class CountingHooks
                {
                    [PipelineInvokable(Hook.PostMain)]
                    public void Count<TMessage>(TMessage message, ErgosfareContext context)
                    {
                    }
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);

        Assert.Contains("Count<global::TestApp.KeyedPing>", result.GeneratedSource);
        Assert.DoesNotContain("Count<global::TestApp.DefaultPing>", result.GeneratedSource);
    }

    /// <summary>
    ///     The shape filter is the generic constraint itself: the call lands only on plans
    ///     whose message satisfies it, with nothing emitted anywhere else. Emitting it wider
    ///     would not merely be wrong — it would not compile.
    /// </summary>
    [Fact]
    public void GenericConstraint_NarrowsTheEmissionToSatisfyingMessages()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Plugins.Abstractions;
            using System.Threading.Tasks;

            namespace TestApp
            {
                public interface IAudited;

                public sealed record AuditedPing : ICommand, IAudited;

                public sealed class AuditedPingHandler : ICommandHandler<AuditedPing>
                {
                    public ValueTask HandleAsync(AuditedPing message, ErgosfareContext context) => default;
                }

                public sealed record PlainPing : ICommand;

                public sealed class PlainPingHandler : ICommandHandler<PlainPing>
                {
                    public ValueTask HandleAsync(PlainPing message, ErgosfareContext context) => default;
                }

                public sealed class AuditHooks
                {
                    [PipelineInvokable(Hook.PostMain)]
                    public void Audit<TMessage>(TMessage message, ErgosfareContext context)
                        where TMessage : IAudited
                    {
                    }
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);

        Assert.Contains("Audit<global::TestApp.AuditedPing>", result.GeneratedSource);
        Assert.DoesNotContain("Audit<global::TestApp.PlainPing>", result.GeneratedSource);
        Assert.DoesNotContain("AddStagedPlan<global::TestApp.PlainPing", result.GeneratedSource);
    }

    /// <summary>
    ///     A dependency the plan cannot name binds to the dispatching provider, which is what
    ///     lets a singleton plugin service take a scoped dependency without capturing it.
    /// </summary>
    [Fact]
    public void UnrecognizedParameter_ResolvesFromTheDispatchingProvider()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Plugins.Abstractions;
            using System.Threading.Tasks;

            namespace TestApp
            {
                public interface IOutboxStore;

                public sealed record OutboxPing : ICommand;

                public sealed class OutboxPingHandler : ICommandHandler<OutboxPing>
                {
                    public ValueTask HandleAsync(OutboxPing message, ErgosfareContext context) => default;
                }

                public sealed class OutboxHooks
                {
                    [PipelineInvokable(Hook.PostMain)]
                    public void Enqueue<TMessage>(TMessage message, ErgosfareContext context, IOutboxStore store)
                    {
                    }
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);

        Assert.Contains(
            ".Enqueue<global::TestApp.OutboxPing>(message, context, "
            + "global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions"
            + ".GetRequiredService<global::TestApp.IOutboxStore>(serviceProvider));",
            result.GeneratedSource);
    }

    /// <summary>
    ///     A static method needs no service at all — the cheapest shape, and the one a plugin
    ///     with no state should reach for.
    /// </summary>
    [Fact]
    public void StaticInvokable_IsCalledWithoutResolvingAService()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Plugins.Abstractions;
            using System.Threading.Tasks;

            namespace TestApp
            {
                public sealed record StaticPing : ICommand;

                public sealed class StaticPingHandler : ICommandHandler<StaticPing>
                {
                    public ValueTask HandleAsync(StaticPing message, ErgosfareContext context) => default;
                }

                public sealed class StaticHooks
                {
                    [PipelineInvokable(Hook.Start)]
                    public static void Started<TMessage>(TMessage message, ErgosfareContext context)
                    {
                    }
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);

        Assert.Contains(
            "global::TestApp.StaticHooks.Started<global::TestApp.StaticPing>(message, context);",
            result.GeneratedSource);
        Assert.DoesNotContain("GetRequiredService<global::TestApp.StaticHooks>", result.GeneratedSource);
    }

    /// <summary>
    ///     Takılı değilken sıfır: a compilation referencing no plugin emits exactly what it
    ///     emits today — here, the single-handler plan the staged family never touches.
    /// </summary>
    [Fact]
    public void WithoutAPlugin_TheInterceptorlessPlanIsUnchanged()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions;
            using System.Threading.Tasks;

            namespace TestApp
            {
                public sealed record BarePing : ICommand;

                public sealed class BarePingHandler : ICommandHandler<BarePing>
                {
                    public ValueTask HandleAsync(BarePing message, ErgosfareContext context) => default;
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);

        Assert.Contains("AddVoidPlan<global::TestApp.BarePing", result.GeneratedSource);
        Assert.DoesNotContain("AddStagedPlan<global::TestApp.BarePing", result.GeneratedSource);
    }
}
