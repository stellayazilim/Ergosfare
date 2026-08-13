namespace Stella.Ergosfare.SourceGenerator.Test;

/// <summary>
///     Plugin call emission: a <c>[PipelineInvokable]</c> method becomes a straight call baked
///     into the plan body at the boundary it declared, closed over the plan's own message and
///     result types. The filters — family, discovery key, generic constraint — are decided at
///     emission, so a plan they exclude carries no call and no runtime check at all.
/// </summary>
/// <remarks>
///     Every test asserts <c>CompilationErrors</c> is empty. That is not ceremony here: the
///     emitted call is closed over concrete types in the consumer's compilation, so a filter
///     that admits a plan it should not is a broken consumer build, and this is where that
///     shows up.
/// </remarks>
public class PluginInvocationEmissionTests
{
    /// <summary>A void command with one pre interceptor, plus a resultless observer.</summary>
    [Fact]
    public void VoidInvokable_IsCalledInTheStagedPlanBody()
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
                    [VoidPipelineInvokable(Stage.PostMainHandler)]
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
    ///     The result-shaped observer is closed over both the message and the result, and reads
    ///     the plan's own result local rather than an erased one.
    /// </summary>
    [Fact]
    public void ResultInvokable_IsClosedOverTheMessageAndResult()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Plugins.Abstractions;
            using Stella.Ergosfare.Queries.Abstractions;
            using System.Threading.Tasks;

            namespace TestApp
            {
                public sealed record CountQuery : IQuery<int>;

                public sealed class CountQueryHandler : IQueryHandler<CountQuery, int>
                {
                    public ValueTask<int> HandleAsync(CountQuery message, ErgosfareContext context)
                        => ValueTask.FromResult(1);
                }

                public sealed class TracingHooks
                {
                    [PipelineInvokable(Stage.PostMainHandler)]
                    public ValueTask ObserveAsync<TMessage, TResult>(TMessage message, TResult result, ErgosfareContext context)
                        => default;
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);

        // A value-typed result crosses the call as an int, not as a boxed object.
        Assert.Contains(
            "await global::Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions"
            + ".GetRequiredService<global::TestApp.TracingHooks>(serviceProvider)"
            + ".ObserveAsync<global::TestApp.CountQuery, int>(message, result, context);",
            result.GeneratedSource);
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
                    [VoidPipelineInvokable(Stage.PreMainHandler)]
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

        // Nobody declared an exception or final observer, so the collapsed body stays the
        // straight line it is today: no try, no catch, no finally.
        Assert.DoesNotContain("catch (", result.GeneratedSource);
        Assert.DoesNotContain("finally", result.GeneratedSource);
    }

    /// <summary>
    ///     The guards grow only for what was declared: a final observer earns the
    ///     <c>finally</c>, and an aborted dispatch skips it exactly as it skips the final
    ///     interceptor stage.
    /// </summary>
    [Fact]
    public void FinalObserver_GrowsTheFinallyOnACollapsedPlan()
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
                    [VoidPipelineInvokable(Stage.OnFinal)]
                    public void Close<TMessage>(TMessage message, ErgosfareContext context)
                    {
                    }
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);

        Assert.Contains("finally", result.GeneratedSource);
        Assert.Contains("if (!aborted)", result.GeneratedSource);
        Assert.Contains(
            "GetRequiredService<global::TestApp.ScopeHooks>(serviceProvider)"
            + ".Close<global::TestApp.ClosingPing>(message, context);",
            result.GeneratedSource);
    }

    /// <summary>
    ///     The exception observer runs ahead of the interceptor stage: an interceptor that
    ///     returns a value swallows the exception, and an observer whose job is to see every
    ///     failure must not depend on whether one did.
    /// </summary>
    [Fact]
    public void ExceptionObserver_RunsBeforeTheInterceptorStage()
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

                public sealed class FailingPingExceptionInterceptor : ICommandExceptionInterceptor<FailingPing>
                {
                    public ValueTask<object> HandleAsync(
                        FailingPing message, object? result, Exception exception, ErgosfareContext context)
                        => ValueTask.FromResult<object>(Unit.Value);
                }

                public sealed class MetricHooks
                {
                    [VoidPipelineInvokable(Stage.OnException)]
                    public void Failed<TMessage>(TMessage message, ErgosfareContext context)
                    {
                    }
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);

        var source = result.GeneratedSource;
        var observer = source.IndexOf(".Failed<global::TestApp.FailingPing>", StringComparison.Ordinal);
        var interceptors = source.IndexOf("var resultBeforeExceptions", StringComparison.Ordinal);

        Assert.True(observer > 0);
        Assert.True(interceptors > 0);
        Assert.True(observer < interceptors);
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
                    [VoidPipelineInvokable(Stage.PostMainHandler)]
                    public void Commit<TMessage>(TMessage message, ErgosfareContext context)
                    {
                    }

                    [PipelineInvokable(Stage.PostMainHandler)]
                    public void CommitWithResult<TMessage, TResult>(TMessage message, TResult result, ErgosfareContext context)
                    {
                    }
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);

        Assert.Contains("Commit<global::TestApp.WritePing>", result.GeneratedSource);
        Assert.DoesNotContain("CommitWithResult<global::TestApp.ReadPing", result.GeneratedSource);
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
                    [VoidPipelineInvokable(Stage.PostMainHandler)]
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
                    [VoidPipelineInvokable(Stage.PostMainHandler)]
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
                    [VoidPipelineInvokable(Stage.PostMainHandler)]
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
                    [VoidPipelineInvokable(Stage.PostMainHandler)]
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
                    [VoidPipelineInvokable(Stage.PipelineStart)]
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
