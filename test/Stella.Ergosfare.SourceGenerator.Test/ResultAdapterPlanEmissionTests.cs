using Microsoft.CodeAnalysis;

namespace Stella.Ergosfare.SourceGenerator.Test;

/// <summary>
/// The staged plans' baked value channel: a result slot with an adapter binding — the
/// native <c>Result</c>/<c>Result&lt;T&gt;</c> carriers or a <c>[ResultAdapter]</c>
/// annotation — gets probe branches after the handler and after every post-interceptor,
/// a materializing catch when the carrier can absorb a failure, and the adapter identity
/// baked into the plan composition. A slot without a binding keeps the classic emission
/// with no probe at all.
/// </summary>
public class ResultAdapterPlanEmissionTests
{
    [Fact]
    public void NativeCarrierSlot_EmitsTheValueChannelAndMaterializes()
    {
        var result = GeneratorTestHost.Run("""
            using System;
            using System.Threading.Tasks;
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Results;

            namespace TestApp
            {
                public sealed record CarrierPing : ICommand<Result<string>>;

                public sealed class CarrierPingHandler : ICommandHandler<CarrierPing, Result<string>>
                {
                    public ValueTask<Result<string>> HandleAsync(CarrierPing message, ErgosfareContext context)
                        => new(Result<string>.Ok("done"));
                }

                public sealed class CarrierPingPost : ICommandPostInterceptor<CarrierPing, Result<string>>
                {
                    public ValueTask<Result<string>> HandleAsync(CarrierPing message, Result<string> messageResult, ErgosfareContext context)
                        => new(messageResult);
                }

                public sealed class CarrierPingExceptionInterceptor : ICommandExceptionInterceptor<CarrierPing>
                {
                    public ValueTask<object> HandleAsync(CarrierPing message, object? messageResult, Exception exception, ErgosfareContext context)
                        => ValueTask.FromResult<object>(messageResult!);
                }

                public sealed class CarrierPingFilteredExceptionInterceptor
                    : ICommandExceptionInterceptorFor<CarrierPing, Result<string>, ArgumentException>
                {
                    public ValueTask<Result<string>> HandleAsync(CarrierPing message, Result<string> messageResult, ArgumentException exception, ErgosfareContext context)
                        => new(messageResult);
                }
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);

        // The plan exists, bakes the built-in adapter's identity, and probes without any
        // adapter instance — the carrier is compiler knowledge.
        Assert.Contains("AddStagedPlan<global::TestApp.CarrierPing, global::Stella.Ergosfare.Core.Abstractions.Results.Result<string>>", result.GeneratedSource);
        Assert.Contains("typeof(global::Stella.Ergosfare.Core.Abstractions.Results.ResultExceptionAdapter<string>)", result.GeneratedSource);
        Assert.Contains("if (result.Exception is { } carriedException)", result.GeneratedSource);
        Assert.Contains("postCarrier0.Exception is { } postException0", result.GeneratedSource);
        Assert.Contains("result = postCarrier0;", result.GeneratedSource);

        // A real throw materializes into a failed carrier; nothing ever rethrows, so the
        // unhandled-failure slot is not emitted at all. The filtered participant keeps
        // its compile-time filter, now testing the channel-agnostic exception local.
        Assert.Contains("result = global::Stella.Ergosfare.Core.Abstractions.Results.Result<string>.Fail(e);", result.GeneratedSource);
        Assert.Contains("if (exception is global::System.ArgumentException)", result.GeneratedSource);
        Assert.DoesNotContain("unhandledException", result.GeneratedSource);
        Assert.DoesNotContain("ResultAdapter.TryGetException", result.GeneratedSource);
    }

    [Fact]
    public void AnnotatedForeignSlot_BakesTheAdapterAndKeepsTheUnhandledRethrow()
    {
        var result = GeneratorTestHost.Run("""
            using System;
            using System.Threading.Tasks;
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Results;
            using Stella.Ergosfare.Core.Abstractions.Attributes;

            namespace TestApp
            {
                public sealed class ForeignOutcome
                {
                    public Exception? Error { get; set; }
                }

                public sealed class ForeignOutcomeAdapter : IResultAdapter<ForeignOutcome>
                {
                    public bool TryGetException(in ForeignOutcome result, out Exception? exception)
                    {
                        exception = result.Error;
                        return exception is not null;
                    }
                }

                [ResultAdapter(typeof(ForeignOutcomeAdapter))]
                public sealed record ForeignPing : ICommand<ForeignOutcome>;

                public sealed class ForeignPingHandler : ICommandHandler<ForeignPing, ForeignOutcome>
                {
                    public ValueTask<ForeignOutcome> HandleAsync(ForeignPing message, ErgosfareContext context)
                        => new(new ForeignOutcome());
                }

                public sealed class ForeignPingFinal : ICommandFinalInterceptor<ForeignPing>
                {
                    public ValueTask HandleAsync(ForeignPing message, object? messageResult, Exception? exception, ErgosfareContext context)
                        => default;
                }
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);

        // The annotated adapter is instantiated once, probes through TryGetException, and
        // — being unable to absorb a failure — keeps the classic unhandled outcome,
        // rethrown after the final stage.
        Assert.Contains("private static readonly global::TestApp.ForeignOutcomeAdapter ResultAdapter = new global::TestApp.ForeignOutcomeAdapter();", result.GeneratedSource);
        Assert.Contains("typeof(global::TestApp.ForeignOutcomeAdapter)", result.GeneratedSource);
        Assert.Contains("if (ResultAdapter.TryGetException(in result, out var carriedException) && carriedException is not null)", result.GeneratedSource);
        Assert.Contains("unhandledException = global::System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception);", result.GeneratedSource);
        Assert.Contains("unhandledException.Throw();", result.GeneratedSource);
        Assert.DoesNotContain(".Fail(e);", result.GeneratedSource);
    }

    [Fact]
    public void MaterializingForeignSlot_MaterializesThroughTheAdapter()
    {
        var result = GeneratorTestHost.Run("""
            using System;
            using System.Threading.Tasks;
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Results;
            using Stella.Ergosfare.Core.Abstractions.Attributes;

            namespace TestApp
            {
                public sealed class AbsorbentOutcome
                {
                    public Exception? Error { get; set; }
                }

                public sealed class AbsorbentOutcomeAdapter : IResultAdapter<AbsorbentOutcome>, IResultMaterializer<AbsorbentOutcome>
                {
                    public bool TryGetException(in AbsorbentOutcome result, out Exception? exception)
                    {
                        exception = result.Error;
                        return exception is not null;
                    }

                    public AbsorbentOutcome Materialize(Exception exception)
                        => new AbsorbentOutcome { Error = exception };
                }

                [ResultAdapter(typeof(AbsorbentOutcomeAdapter))]
                public sealed record AbsorbentPing : ICommand<AbsorbentOutcome>;

                public sealed class AbsorbentPingHandler : ICommandHandler<AbsorbentPing, AbsorbentOutcome>
                {
                    public ValueTask<AbsorbentOutcome> HandleAsync(AbsorbentPing message, ErgosfareContext context)
                        => new(new AbsorbentOutcome());
                }

                public sealed class AbsorbentPingFinal : ICommandFinalInterceptor<AbsorbentPing>
                {
                    public ValueTask HandleAsync(AbsorbentPing message, object? messageResult, Exception? exception, ErgosfareContext context)
                        => default;
                }
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);

        Assert.Contains("result = ResultAdapter.Materialize(e);", result.GeneratedSource);
        Assert.DoesNotContain("unhandledException", result.GeneratedSource);
    }

    [Fact]
    public void UnadaptedSlot_KeepsTheClassicEmissionWithNoProbe()
    {
        var result = GeneratorTestHost.Run("""
            using System;
            using System.Threading.Tasks;
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Results;

            namespace TestApp
            {
                public sealed record PlainPing : ICommand<string>;

                public sealed class PlainPingHandler : ICommandHandler<PlainPing, string>
                {
                    public ValueTask<string> HandleAsync(PlainPing message, ErgosfareContext context)
                        => new("done");
                }

                public sealed class PlainPingPost : ICommandPostInterceptor<PlainPing, string>
                {
                    public ValueTask<string> HandleAsync(PlainPing message, string messageResult, ErgosfareContext context)
                        => new(messageResult);
                }
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);

        Assert.Contains("AddStagedPlan<global::TestApp.PlainPing, string>", result.GeneratedSource);
        Assert.DoesNotContain("carriedException", result.GeneratedSource);
        Assert.DoesNotContain("unhandledException", result.GeneratedSource);

        // Nothing binds the slot, so the adapter table holds no entry for it — only the
        // seal, which every generated registration writes.
        Assert.DoesNotContain("AddResultAdapter<", result.GeneratedSource);
        Assert.DoesNotContain("AddDefaultResultAdapter<", result.GeneratedSource);
        Assert.DoesNotContain("ResultAdapter.TryGetException", result.GeneratedSource);
    }

    [Fact]
    public void UnitFittingAnnotation_DisqualifiesTheVoidPlan()
    {
        var result = GeneratorTestHost.Run("""
            using System;
            using System.Threading.Tasks;
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Results;
            using Stella.Ergosfare.Core.Abstractions.Attributes;

            namespace TestApp
            {
                public sealed class UnitAdapter : IResultAdapter<Unit>
                {
                    public bool TryGetException(in Unit result, out Exception? exception)
                    {
                        exception = null;
                        return false;
                    }
                }

                [ResultAdapter(typeof(UnitAdapter))]
                public sealed record UnitAnnotatedPing : ICommand;

                public sealed class UnitAnnotatedPingHandler : ICommandHandler<UnitAnnotatedPing>
                {
                    public ValueTask HandleAsync(UnitAnnotatedPing message, ErgosfareContext context)
                        => default;
                }

                public sealed class UnitAnnotatedPingPre : ICommandPreInterceptor<UnitAnnotatedPing>
                {
                    public ValueTask<UnitAnnotatedPing> HandleAsync(UnitAnnotatedPing message, ErgosfareContext context)
                        => ValueTask.FromResult(message);
                }
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);

        // A Unit-fitting annotation binds the void lane at runtime; void plans model no
        // adapter, so the message stays off the staged store and the strategy probes it.
        Assert.DoesNotContain("AddStagedPlan<global::TestApp.UnitAnnotatedPing>", result.GeneratedSource);
    }

    [Fact]
    public void OptedOutNativeCarrierSlot_KeepsTheClassicEmission()
    {
        var result = GeneratorTestHost.Run("""
            using System;
            using System.Threading.Tasks;
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Results;
            using Stella.Ergosfare.Core.Abstractions.Attributes;

            namespace TestApp
            {
                [IgnoreResultAdapter]
                public sealed record OptedOutPing : ICommand<Result<string>>;

                public sealed class OptedOutPingHandler : ICommandHandler<OptedOutPing, Result<string>>
                {
                    public ValueTask<Result<string>> HandleAsync(OptedOutPing message, ErgosfareContext context)
                        => new(Result<string>.Ok("done"));
                }

                public sealed class OptedOutPingPost : ICommandPostInterceptor<OptedOutPing, Result<string>>
                {
                    public ValueTask<Result<string>> HandleAsync(OptedOutPing message, Result<string> messageResult, ErgosfareContext context)
                        => new(messageResult);
                }
            }
            """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);

        // The opt-out suppresses the native tier: the plan is still emitted, carries no
        // baked adapter, and probes nothing — the classic shape, byte for byte.
        Assert.Contains("AddStagedPlan<global::TestApp.OptedOutPing, global::Stella.Ergosfare.Core.Abstractions.Results.Result<string>>", result.GeneratedSource);
        Assert.DoesNotContain("carriedException", result.GeneratedSource);
        Assert.DoesNotContain(".Fail(e);", result.GeneratedSource);
        Assert.DoesNotContain("typeof(global::Stella.Ergosfare.Core.Abstractions.Results.ResultExceptionAdapter", result.GeneratedSource);
    }

    private const string DefaultAdapterBoot = """
        using System;
        using System.Threading.Tasks;
        using Stella.Ergosfare.Commands.Abstractions;
        using Stella.Ergosfare.Core.Abstractions;
        using Stella.Ergosfare.Core.Abstractions.Results;
        using Stella.Ergosfare.Core.Abstractions.Attributes;
        using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

        namespace TestApp
        {
            public sealed class Box<T>
            {
                public Exception? Error { get; set; }
            }

            public sealed class BoxAdapter<T> : IResultAdapter<Box<T>>, IResultMaterializer<Box<T>>
            {
                public bool TryGetException(in Box<T> result, out Exception? exception)
                {
                    exception = result.Error;
                    return exception is not null;
                }

                public Box<T> Materialize(Exception exception)
                    => new Box<T> { Error = exception };
            }

            public static class Boot
            {
                public static void Configure(IModuleRegistry registry)
                    => registry.UseDefaultResultAdapter(typeof(BoxAdapter<>));
            }
        """;

    [Fact]
    public void DiscoveredOpenGenericDefault_IsClosedOverTheSlotAndBaked()
    {
        var result = GeneratorTestHost.Run(DefaultAdapterBoot + """

            public sealed record BoxPing : ICommand<Box<int>>;

            public sealed class BoxPingHandler : ICommandHandler<BoxPing, Box<int>>
            {
                public ValueTask<Box<int>> HandleAsync(BoxPing message, ErgosfareContext context)
                    => new(new Box<int>());
            }

            public sealed class BoxPingFinal : ICommandFinalInterceptor<BoxPing>
            {
                public ValueTask HandleAsync(BoxPing message, object? messageResult, Exception? exception, ErgosfareContext context)
                    => default;
            }
        }
        """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);

        // The default's open definition unifies with the slot, closes over it, and the
        // materializer facet survives the closing.
        Assert.Contains("private static readonly global::TestApp.BoxAdapter<int> ResultAdapter = new global::TestApp.BoxAdapter<int>();", result.GeneratedSource);
        Assert.Contains("typeof(global::TestApp.BoxAdapter<int>)", result.GeneratedSource);
        Assert.Contains("if (ResultAdapter.TryGetException(in result, out var carriedException) && carriedException is not null)", result.GeneratedSource);
        Assert.Contains("result = ResultAdapter.Materialize(e);", result.GeneratedSource);
    }

    [Fact]
    public void AnnotationNativeAndOptOut_AllWinOverTheDiscoveredDefault()
    {
        var result = GeneratorTestHost.Run(DefaultAdapterBoot + """

            // Native slot: the built-in adapter is baked, not the default.
            public sealed record NativePing : ICommand<Result<string>>;

            public sealed class NativePingHandler : ICommandHandler<NativePing, Result<string>>
            {
                public ValueTask<Result<string>> HandleAsync(NativePing message, ErgosfareContext context)
                    => new(Result<string>.Ok("done"));
            }

            public sealed class NativePingFinal : ICommandFinalInterceptor<NativePing>
            {
                public ValueTask HandleAsync(NativePing message, object? messageResult, Exception? exception, ErgosfareContext context)
                    => default;
            }

            // Opted out: no adapter at all, although the default could serve the slot.
            [IgnoreResultAdapter]
            public sealed record OptedOutBoxPing : ICommand<Box<string>>;

            public sealed class OptedOutBoxPingHandler : ICommandHandler<OptedOutBoxPing, Box<string>>
            {
                public ValueTask<Box<string>> HandleAsync(OptedOutBoxPing message, ErgosfareContext context)
                    => new(new Box<string>());
            }

            public sealed class OptedOutBoxPingFinal : ICommandFinalInterceptor<OptedOutBoxPing>
            {
                public ValueTask HandleAsync(OptedOutBoxPing message, object? messageResult, Exception? exception, ErgosfareContext context)
                    => default;
            }

            // A slot the default cannot unify with: no adapter, classic emission — and
            // the visible ERGO013 acknowledgment request.
            public sealed record UnservedPing : ICommand<string>;

            public sealed class UnservedPingHandler : ICommandHandler<UnservedPing, string>
            {
                public ValueTask<string> HandleAsync(UnservedPing message, ErgosfareContext context)
                    => new("done");
            }

            public sealed class UnservedPingFinal : ICommandFinalInterceptor<UnservedPing>
            {
                public ValueTask HandleAsync(UnservedPing message, object? messageResult, Exception? exception, ErgosfareContext context)
                    => default;
            }
        }
        """);

        Assert.Empty(result.CompilationErrors);

        // The unserved, unannotated slot is the only diagnostic — the compile-time
        // design-hole error. The native slot and the opted-out (default-served) slot
        // stay silent.
        var diagnostic = Assert.Single(result.GeneratorDiagnostics);
        Assert.Equal("ERGO013", diagnostic.Id);

        // Native slot bakes the built-in adapter; the ignored and unserved slots bake
        // nothing.
        Assert.Contains("typeof(global::Stella.Ergosfare.Core.Abstractions.Results.ResultExceptionAdapter<string>)", result.GeneratedSource);
        Assert.DoesNotContain("BoxAdapter<string>", result.GeneratedSource);
        Assert.DoesNotContain("BoxAdapter<int>", result.GeneratedSource);
    }

    [Fact]
    public void EveryTier_ReachesTheGeneratedAdapterTable()
    {
        var result = GeneratorTestHost.Run(DefaultAdapterBoot + """

            // The fallback serves this one.
            public sealed record BoxTablePing : ICommand<Box<int>>;

            public sealed class BoxTablePingHandler : ICommandHandler<BoxTablePing, Box<int>>
            {
                public ValueTask<Box<int>> HandleAsync(BoxTablePing message, ErgosfareContext context)
                    => new(new Box<int>());
            }

            // An annotation names its own, for its own slot.
            public sealed class Outcome
            {
                public Exception? Error { get; set; }
            }

            public sealed class OutcomeAdapter : IResultAdapter<Outcome>
            {
                public bool TryGetException(in Outcome result, out Exception? exception)
                {
                    exception = result.Error;
                    return exception is not null;
                }
            }

            [ResultAdapter(typeof(OutcomeAdapter))]
            public sealed record AnnotatedTablePing : ICommand<Outcome>;

            public sealed class AnnotatedTablePingHandler : ICommandHandler<AnnotatedTablePing, Outcome>
            {
                public ValueTask<Outcome> HandleAsync(AnnotatedTablePing message, ErgosfareContext context)
                    => new(new Outcome());
            }

            // And this one wants no tier at all.
            [IgnoreResultAdapter]
            public sealed record OptedOutTablePing : ICommand<Box<string>>;

            public sealed class OptedOutTablePingHandler : ICommandHandler<OptedOutTablePing, Box<string>>
            {
                public ValueTask<Box<string>> HandleAsync(OptedOutTablePing message, ErgosfareContext context)
                    => new(new Box<string>());
            }
        }
        """);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);

        // The annotation tier, per (message, slot). The adapter is a type argument, so the
        // compiler is what checks that it serves the slot.
        Assert.Contains(
            "AddResultAdapter<global::TestApp.AnnotatedTablePing, global::TestApp.Outcome, global::TestApp.OutcomeAdapter>();",
            result.GeneratedSource);

        // The opt-out, per message — and no slot entry for it, the entry being the whole
        // answer.
        Assert.Contains("AddIgnoredResultAdapter<global::TestApp.OptedOutTablePing>();", result.GeneratedSource);
        Assert.DoesNotContain("AddDefaultResultAdapter<global::TestApp.Box<string>", result.GeneratedSource);

        // The fallback tier, per result type, already closed over the slot.
        Assert.Contains(
            "AddDefaultResultAdapter<global::TestApp.Box<int>, global::TestApp.BoxAdapter<int>>();",
            result.GeneratedSource);

        // A command's void dispatch binds over Unit, and nothing here serves it.
        Assert.DoesNotContain("AddDefaultResultAdapter<global::Stella.Ergosfare.Core.Abstractions.Unit", result.GeneratedSource);

        // Sealed: past this, a slot missing from the table is an answer.
        Assert.Contains("SealResultAdapters();", result.GeneratedSource);
    }

    [Fact]
    public void OpaqueDefaultCallsite_FailsTheBuild()
    {
        var result = GeneratorTestHost.Run(DefaultAdapterBoot.Replace(
            "=> registry.UseDefaultResultAdapter(typeof(BoxAdapter<>));",
            """
            {
                var adapterType = typeof(BoxAdapter<>);
                registry.UseDefaultResultAdapter(adapterType);
            }
            """) + """

            public sealed record OpaqueBoxPing : ICommand<Box<int>>;

            public sealed class OpaqueBoxPingHandler : ICommandHandler<OpaqueBoxPing, Box<int>>
            {
                public ValueTask<Box<int>> HandleAsync(OpaqueBoxPing message, ErgosfareContext context)
                    => new(new Box<int>());
            }

            public sealed class OpaqueBoxPingFinal : ICommandFinalInterceptor<OpaqueBoxPing>
            {
                public ValueTask HandleAsync(OpaqueBoxPing message, object? messageResult, Exception? exception, ErgosfareContext context)
                    => default;
            }
        }
        """);

        Assert.Empty(result.CompilationErrors);

        // The argument is a variable, so which result types the fallback serves cannot be
        // read here — and nothing reads it anywhere else. The call is the error.
        var diagnostic = Assert.Single(result.GeneratorDiagnostics, d => d.Id == "ERGO019");
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);

        // Nothing is bound for the slot: no plan adapter, and no table entry.
        Assert.DoesNotContain("typeof(global::TestApp.BoxAdapter<int>)", result.GeneratedSource);
        Assert.DoesNotContain("AddDefaultResultAdapter<", result.GeneratedSource);
    }

    [Fact]
    public void DisagreeingDefaultCallsites_FailTheBuild()
    {
        var result = GeneratorTestHost.Run(DefaultAdapterBoot.Replace(
            "=> registry.UseDefaultResultAdapter(typeof(BoxAdapter<>));",
            """
            {
                registry.UseDefaultResultAdapter(typeof(BoxAdapter<>));
                registry.UseDefaultResultAdapter(typeof(OtherAdapter));
            }
            """) + """

            public sealed class Other
            {
                public Exception? Error { get; set; }
            }

            public sealed class OtherAdapter : IResultAdapter<Other>
            {
                public bool TryGetException(in Other result, out Exception? exception)
                {
                    exception = result.Error;
                    return exception is not null;
                }
            }

            public sealed record TwoDefaultsPing : ICommand<Box<int>>;

            public sealed class TwoDefaultsPingHandler : ICommandHandler<TwoDefaultsPing, Box<int>>
            {
                public ValueTask<Box<int>> HandleAsync(TwoDefaultsPing message, ErgosfareContext context)
                    => new(new Box<int>());
            }
        }
        """);

        Assert.Empty(result.CompilationErrors);

        // Two answers to one question. The second call is where it is reported, and the
        // message names both adapters so either site can be the one that moves.
        var diagnostic = Assert.Single(result.GeneratorDiagnostics, d => d.Id == "ERGO020");
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("OtherAdapter", diagnostic.GetMessage());
        Assert.Contains("BoxAdapter", diagnostic.GetMessage());
    }
}
