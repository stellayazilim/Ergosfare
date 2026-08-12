using Microsoft.CodeAnalysis;

namespace Stella.Ergosfare.SourceGenerator.Test;

/// <summary>
/// ERGOSG011: a <c>[ResultAdapter]</c> annotation that can never bind — the adapter fits
/// none of the message's runtime-probed result slots, or the runtime binding could not
/// instantiate it — fails the build where the message is compiled. A fitting, instantiable
/// annotation stays silent, including one inherited from a base message type.
/// </summary>
public class ResultAdapterDiagnosticsTests
{
    private const string CarrierTypes = """
        using System;
        using System.Threading.Tasks;
        using Stella.Ergosfare.Commands.Abstractions;
        using Stella.Ergosfare.Core.Abstractions;
        using Stella.Ergosfare.Core.Abstractions.Attributes;

        namespace TestApp
        {
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
        """;

    private static void AssertSingle011(GeneratorTestHost.GeneratorRunResult result)
    {
        var diagnostic = Assert.Single(result.GeneratorDiagnostics, d => d.Id == "ERGOSG011");
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
    }

    [Fact]
    public void FittingAnnotation_StaysSilent()
    {
        var result = GeneratorTestHost.Run(CarrierTypes + """

            [ResultAdapter(typeof(OutcomeAdapter))]
            public sealed record AnnotatedPing : ICommand<Outcome>;

            public sealed class AnnotatedPingHandler : ICommandHandler<AnnotatedPing, Outcome>
            {
                public ValueTask<Outcome> HandleAsync(AnnotatedPing message, ErgosfareContext context)
                    => new(new Outcome());
            }
        }
        """);

        Assert.Empty(result.CompilationErrors);
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id == "ERGOSG011");
    }

    [Fact]
    public void MismatchedSlot_FailsTheBuild()
    {
        var result = GeneratorTestHost.Run(CarrierTypes + """

            // The message's declared result is string; the adapter serves Outcome only.
            [ResultAdapter(typeof(OutcomeAdapter))]
            public sealed record MismatchedPing : ICommand<string>;

            public sealed class MismatchedPingHandler : ICommandHandler<MismatchedPing, string>
            {
                public ValueTask<string> HandleAsync(MismatchedPing message, ErgosfareContext context)
                    => new("done");
            }
        }
        """);

        AssertSingle011(result);
    }

    [Fact]
    public void AdapterWithoutPublicParameterlessConstructor_FailsTheBuild()
    {
        var result = GeneratorTestHost.Run(CarrierTypes + """

            public sealed class DependentAdapter : IResultAdapter<Outcome>
            {
                private readonly string _dependency;

                public DependentAdapter(string dependency) => _dependency = dependency;

                public bool TryGetException(in Outcome result, out Exception? exception)
                {
                    exception = result.Error;
                    return exception is not null;
                }
            }

            [ResultAdapter(typeof(DependentAdapter))]
            public sealed record DependentPing : ICommand<Outcome>;

            public sealed class DependentPingHandler : ICommandHandler<DependentPing, Outcome>
            {
                public ValueTask<Outcome> HandleAsync(DependentPing message, ErgosfareContext context)
                    => new(new Outcome());
            }
        }
        """);

        AssertSingle011(result);
    }

    [Fact]
    public void AbstractAdapter_FailsTheBuild()
    {
        var result = GeneratorTestHost.Run(CarrierTypes + """

            public abstract class AbstractAdapter : IResultAdapter<Outcome>
            {
                public bool TryGetException(in Outcome result, out Exception? exception)
                {
                    exception = result.Error;
                    return exception is not null;
                }
            }

            [ResultAdapter(typeof(AbstractAdapter))]
            public sealed record AbstractPing : ICommand<Outcome>;

            public sealed class AbstractPingHandler : ICommandHandler<AbstractPing, Outcome>
            {
                public ValueTask<Outcome> HandleAsync(AbstractPing message, ErgosfareContext context)
                    => new(new Outcome());
            }
        }
        """);

        AssertSingle011(result);
    }

    [Fact]
    public void AnnotationOnAnEvent_FailsTheBuild()
    {
        var result = GeneratorTestHost.Run(CarrierTypes.Replace(
            "using Stella.Ergosfare.Commands.Abstractions;",
            "using Stella.Ergosfare.Events.Abstractions;") + """

            // Broadcast pipelines probe no result slot: the annotation can never bind.
            [ResultAdapter(typeof(OutcomeAdapter))]
            public sealed record AnnotatedEvent : IEvent;

            public sealed class AnnotatedEventHandler : IEventHandler<AnnotatedEvent>
            {
                public ValueTask HandleAsync(AnnotatedEvent message, ErgosfareContext context)
                    => default;
            }
        }
        """);

        AssertSingle011(result);
    }

    [Fact]
    public void InheritedFittingAnnotation_BindsOnTheDerivedMessageAndStaysSilent()
    {
        var result = GeneratorTestHost.Run(CarrierTypes + """

            [ResultAdapter(typeof(OutcomeAdapter))]
            public abstract record CarrierPingBase;

            public sealed record DerivedPing : CarrierPingBase, ICommand<Outcome>;

            public sealed class DerivedPingHandler : ICommandHandler<DerivedPing, Outcome>
            {
                public ValueTask<Outcome> HandleAsync(DerivedPing message, ErgosfareContext context)
                    => new(new Outcome());
            }
        }
        """);

        Assert.Empty(result.CompilationErrors);
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id == "ERGOSG011");
    }

    [Fact]
    public void InheritedMismatchedAnnotation_FailsOnTheDerivedMessage()
    {
        var result = GeneratorTestHost.Run(CarrierTypes + """

            [ResultAdapter(typeof(OutcomeAdapter))]
            public abstract record MismatchedBase;

            public sealed record DerivedMismatchedPing : MismatchedBase, ICommand<string>;

            public sealed class DerivedMismatchedPingHandler : ICommandHandler<DerivedMismatchedPing, string>
            {
                public ValueTask<string> HandleAsync(DerivedMismatchedPing message, ErgosfareContext context)
                    => new("done");
            }
        }
        """);

        AssertSingle011(result);
    }

    [Fact]
    public void BothAnnotations_OnTheSameMessage_FailTheBuild()
    {
        var result = GeneratorTestHost.Run(CarrierTypes + """

            [ResultAdapter(typeof(OutcomeAdapter))]
            [IgnoreResultAdapter]
            public sealed record ConflictedPing : ICommand<Outcome>;

            public sealed class ConflictedPingHandler : ICommandHandler<ConflictedPing, Outcome>
            {
                public ValueTask<Outcome> HandleAsync(ConflictedPing message, ErgosfareContext context)
                    => new(new Outcome());
            }
        }
        """);

        var diagnostic = Assert.Single(result.GeneratorDiagnostics, d => d.Id == "ERGOSG012");
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id == "ERGOSG011");
    }

    [Fact]
    public void InheritedAnnotationWithOwnOptOut_FailsTheBuild()
    {
        var result = GeneratorTestHost.Run(CarrierTypes + """

            [ResultAdapter(typeof(OutcomeAdapter))]
            public abstract record AdaptedBase;

            // The contradiction spans levels: the base declares an adapter, the derived
            // message opts out — still both, still an error.
            [IgnoreResultAdapter]
            public sealed record OptedOutDerivedPing : AdaptedBase, ICommand<Outcome>;

            public sealed class OptedOutDerivedPingHandler : ICommandHandler<OptedOutDerivedPing, Outcome>
            {
                public ValueTask<Outcome> HandleAsync(OptedOutDerivedPing message, ErgosfareContext context)
                    => new(new Outcome());
            }
        }
        """);

        var diagnostic = Assert.Single(result.GeneratorDiagnostics, d => d.Id == "ERGOSG012");
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
    }

    [Fact]
    public void OptOutAlone_StaysSilent()
    {
        var result = GeneratorTestHost.Run(CarrierTypes + """

            [IgnoreResultAdapter]
            public sealed record QuietPing : ICommand<Outcome>;

            public sealed class QuietPingHandler : ICommandHandler<QuietPing, Outcome>
            {
                public ValueTask<Outcome> HandleAsync(QuietPing message, ErgosfareContext context)
                    => new(new Outcome());
            }
        }
        """);

        Assert.Empty(result.CompilationErrors);
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id is "ERGOSG011" or "ERGOSG012");
    }

    private const string DefaultAdapterBoot = """
        using System;
        using System.Threading.Tasks;
        using Stella.Ergosfare.Commands.Abstractions;
        using Stella.Ergosfare.Core.Abstractions;
        using Stella.Ergosfare.Core.Abstractions.Attributes;
        using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

        namespace TestApp
        {
            public sealed class User { }

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

            public static class Boot
            {
                public static void Configure(IModuleRegistry registry)
                    => registry.UseDefaultResultAdapter(typeof(OutcomeAdapter));
            }
        """;

    [Fact]
    public void UnservedUnannotatedMessage_FailsTheBuildBeforeAnyDispatch()
    {
        var result = GeneratorTestHost.Run(DefaultAdapterBoot + """

            // The default cannot extract failures from User and nothing acknowledges
            // that: the design hole fails the build — no dispatch needed to reveal it.
            public sealed record UserCreateCmd : ICommand<User>;

            public sealed class UserCreateHandler : ICommandHandler<UserCreateCmd, User>
            {
                public ValueTask<User> HandleAsync(UserCreateCmd message, ErgosfareContext context)
                    => new(new User());
            }
        }
        """);

        Assert.Empty(result.CompilationErrors);

        var diagnostic = Assert.Single(result.GeneratorDiagnostics, d => d.Id == "ERGOSG013");
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
    }

    [Fact]
    public void IgnoreResultAdapter_DowngradesTheUnservedFindingToAWarning()
    {
        var result = GeneratorTestHost.Run(DefaultAdapterBoot + """

            // Unadaptable result, acknowledged: the deliberate throwing pipeline stays
            // visible as a warning, never an error.
            [IgnoreResultAdapter]
            public sealed record UserCreateCmd : ICommand<User>;

            public sealed class UserCreateHandler : ICommandHandler<UserCreateCmd, User>
            {
                public ValueTask<User> HandleAsync(UserCreateCmd message, ErgosfareContext context)
                    => new(new User());
            }

            // Adaptable result (the default serves Outcome), opted out anyway: the
            // classic lane is a legal, silent choice where the value channel could bind.
            [IgnoreResultAdapter]
            public sealed record OutcomeCmd : ICommand<Outcome>;

            public sealed class OutcomeCmdHandler : ICommandHandler<OutcomeCmd, Outcome>
            {
                public ValueTask<Outcome> HandleAsync(OutcomeCmd message, ErgosfareContext context)
                    => new(new Outcome());
            }
        }
        """);

        Assert.Empty(result.CompilationErrors);

        var diagnostic = Assert.Single(result.GeneratorDiagnostics);
        Assert.Equal("ERGOSG014", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
    }

    [Fact]
    public void ServedAndNativeResults_NeverWarn()
    {
        var result = GeneratorTestHost.Run(DefaultAdapterBoot + """

            // Compatible with the default adapter — nothing to say.
            public sealed record ServedCmd : ICommand<Outcome>;

            public sealed class ServedCmdHandler : ICommandHandler<ServedCmd, Outcome>
            {
                public ValueTask<Outcome> HandleAsync(ServedCmd message, ErgosfareContext context)
                    => new(new Outcome());
            }

            // The native carrier is always served by its built-in adapter.
            public sealed record NativeCmd : ICommand<Result<User>>;

            public sealed class NativeCmdHandler : ICommandHandler<NativeCmd, Result<User>>
            {
                public ValueTask<Result<User>> HandleAsync(NativeCmd message, ErgosfareContext context)
                    => new(Result<User>.Ok(new User()));
            }

            // Void commands have no result value to carry a failure in: throwing is
            // their inherent contract, not a misfit.
            public sealed record VoidCmd : ICommand;

            public sealed class VoidCmdHandler : ICommandHandler<VoidCmd>
            {
                public ValueTask HandleAsync(VoidCmd message, ErgosfareContext context)
                    => default;
            }
        }
        """);

        Assert.Empty(result.CompilationErrors);
        Assert.Empty(result.GeneratorDiagnostics);
    }
}
