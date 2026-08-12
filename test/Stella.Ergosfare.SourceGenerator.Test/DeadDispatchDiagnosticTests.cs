using Microsoft.CodeAnalysis;

namespace Stella.Ergosfare.SourceGenerator.Test;

/// <summary>
///     Dead-dispatch verdicts (ERGOSG005/006) and their gates: a dispatch whose static
///     message type — and every closure subtype of it — lacks a covering handler is a
///     compile error at the composition root; a concrete static type covered only through
///     its subtypes warns; events, non-root compilations and disabled reference scanning
///     stay silent. The opacity aid (ERGOSG009) is off by default and rises with an
///     editorconfig-style severity override.
/// </summary>
public class DeadDispatchDiagnosticTests
{
    private static readonly IReadOnlyDictionary<string, string> CompositionRoot =
        new Dictionary<string, string> { ["ErgosfareCompositionRoot"] = "true" };

    [Fact]
    public void UncoveredCommandDispatch_FailsTheBuildAtTheRoot()
    {
        var result = GeneratorTestHost.Run("""
            using System.Threading.Tasks;
            using Stella.Ergosfare.Commands.Abstractions;

            namespace TestApp
            {
                public sealed record OrphanCommand : ICommand;

                public class Caller(ICommandMediator mediator)
                {
                    public ValueTask Fire(OrphanCommand command) => mediator.SendAsync(command);
                }
            }
            """,
            buildProperties: CompositionRoot);

        Assert.Empty(result.CompilationErrors);

        var diagnostic = Assert.Single(result.GeneratorDiagnostics);
        Assert.Equal("ERGOSG005", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("OrphanCommand", diagnostic.GetMessage());
        Assert.NotEqual(Location.None, diagnostic.Location);
    }

    [Fact]
    public void CoveredCommandDispatch_IsSilent()
    {
        var result = GeneratorTestHost.Run("""
            using System.Threading.Tasks;
            using Stella.Ergosfare.Commands.Abstractions;

            namespace TestApp
            {
                public sealed record Ping : ICommand;

                public sealed class PingHandler : ICommandHandler<Ping>
                {
                    public ValueTask HandleAsync(Ping message, Stella.Ergosfare.Core.Abstractions.ErgosfareContext context)
                        => default;
                }

                public class Caller(ICommandMediator mediator)
                {
                    public ValueTask Fire(Ping ping) => mediator.SendAsync(ping);
                }
            }
            """,
            buildProperties: CompositionRoot);

        Assert.Empty(result.CompilationErrors);
        Assert.Empty(result.GeneratorDiagnostics);
    }

    [Fact]
    public void CovariantBaseHandler_CoversTheDerivedDispatch()
    {
        var result = GeneratorTestHost.Run("""
            using System.Threading.Tasks;
            using Stella.Ergosfare.Commands.Abstractions;

            namespace TestApp
            {
                public abstract record StarCommand : ICommand;

                public sealed record MagnetarCommand : StarCommand;

                public sealed class StarHandler : ICommandHandler<StarCommand>
                {
                    public ValueTask HandleAsync(StarCommand message, Stella.Ergosfare.Core.Abstractions.ErgosfareContext context)
                        => default;
                }

                public class Caller(ICommandMediator mediator)
                {
                    public ValueTask Fire(MagnetarCommand command) => mediator.SendAsync((ICommand)command);
                }
            }
            """,
            buildProperties: CompositionRoot);

        Assert.Empty(result.CompilationErrors);

        // The cast is looked through to MagnetarCommand, whose assignable chain reaches
        // the StarCommand registration — the runtime's covariant resolution, mirrored.
        Assert.Empty(result.GeneratorDiagnostics);
    }

    [Fact]
    public void SubtypeOnlyCoverage_WarnsForTheConcreteStaticType()
    {
        var result = GeneratorTestHost.Run("""
            using System.Threading.Tasks;
            using Stella.Ergosfare.Commands.Abstractions;

            namespace TestApp
            {
                public record StarCommand : ICommand;

                public sealed record MagnetarCommand : StarCommand;

                public sealed class MagnetarHandler : ICommandHandler<MagnetarCommand>
                {
                    public ValueTask HandleAsync(MagnetarCommand message, Stella.Ergosfare.Core.Abstractions.ErgosfareContext context)
                        => default;
                }

                public class Caller(ICommandMediator mediator)
                {
                    public ValueTask Fire(StarCommand command) => mediator.SendAsync(command);
                }
            }
            """,
            buildProperties: CompositionRoot);

        Assert.Empty(result.CompilationErrors);

        var diagnostic = Assert.Single(result.GeneratorDiagnostics);
        Assert.Equal("ERGOSG006", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("StarCommand", diagnostic.GetMessage());
        Assert.Contains("MagnetarCommand", diagnostic.GetMessage());
    }

    [Fact]
    public void AbstractStaticTypeWithCoveredSubtype_IsSilent()
    {
        var result = GeneratorTestHost.Run("""
            using System.Threading.Tasks;
            using Stella.Ergosfare.Commands.Abstractions;

            namespace TestApp
            {
                public abstract record StarCommand : ICommand;

                public sealed record MagnetarCommand : StarCommand;

                public sealed class MagnetarHandler : ICommandHandler<MagnetarCommand>
                {
                    public ValueTask HandleAsync(MagnetarCommand message, Stella.Ergosfare.Core.Abstractions.ErgosfareContext context)
                        => default;
                }

                public class Caller(ICommandMediator mediator)
                {
                    public ValueTask Fire(StarCommand command) => mediator.SendAsync(command);
                }
            }
            """,
            buildProperties: CompositionRoot);

        Assert.Empty(result.CompilationErrors);

        // An abstract static type can only ever carry a subtype instance; a covered
        // subtype makes the site ordinary polymorphic dispatch.
        Assert.Empty(result.GeneratorDiagnostics);
    }

    [Fact]
    public void PublishingAnEventWithoutSubscribers_IsALegalNoOp()
    {
        var result = GeneratorTestHost.Run("""
            using System.Threading.Tasks;
            using Stella.Ergosfare.Events.Abstractions;

            namespace TestApp
            {
                public sealed record Blinked : IEvent;

                public class Caller(IEventMediator mediator)
                {
                    public ValueTask Fire(Blinked e) => mediator.PublishAsync(e);
                }
            }
            """,
            buildProperties: CompositionRoot);

        Assert.Empty(result.CompilationErrors);
        Assert.Empty(result.GeneratorDiagnostics);
    }

    [Fact]
    public void NonRootCompilation_EmitsTheManifestButNoVerdicts()
    {
        var result = GeneratorTestHost.Run("""
            using System.Threading.Tasks;
            using Stella.Ergosfare.Commands.Abstractions;

            namespace TestApp
            {
                public sealed record OrphanCommand : ICommand;

                public class Caller(ICommandMediator mediator)
                {
                    public ValueTask Fire(OrphanCommand command) => mediator.SendAsync(command);
                }
            }
            """);

        Assert.Empty(result.CompilationErrors);
        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Contains("\"TestApp.OrphanCommand\"", result.GeneratedSource);
    }

    [Fact]
    public void ExecutableOutput_IsARootByDefault()
    {
        var result = GeneratorTestHost.Run("""
            using System.Threading.Tasks;
            using Stella.Ergosfare.Commands.Abstractions;

            namespace TestApp
            {
                public sealed record OrphanCommand : ICommand;

                public class Caller(ICommandMediator mediator)
                {
                    public ValueTask Fire(OrphanCommand command) => mediator.SendAsync(command);
                }

                public static class Program
                {
                    public static void Main() { }
                }
            }
            """,
            outputKind: Microsoft.CodeAnalysis.OutputKind.ConsoleApplication);

        Assert.Empty(result.CompilationErrors);

        var diagnostic = Assert.Single(result.GeneratorDiagnostics);
        Assert.Equal("ERGOSG005", diagnostic.Id);
    }

    [Fact]
    public void DisabledReferenceScanning_SuspendsVerdicts()
    {
        var result = GeneratorTestHost.Run("""
            using System.Threading.Tasks;
            using Stella.Ergosfare.Commands.Abstractions;

            namespace TestApp
            {
                public sealed record OrphanCommand : ICommand;

                public class Caller(ICommandMediator mediator)
                {
                    public ValueTask Fire(OrphanCommand command) => mediator.SendAsync(command);
                }
            }
            """,
            scanReferences: false,
            buildProperties: CompositionRoot);

        Assert.Empty(result.CompilationErrors);

        // With scanning off the composition is deliberately incomplete; no closure claim
        // is sound.
        Assert.Empty(result.GeneratorDiagnostics);
    }

    [Fact]
    public void DeadDispatchRecordedInAReferencedManifest_FailsTheRootBuild()
    {
        var result = GeneratorTestHost.Run("""
            namespace TestApp
            {
                public sealed class JustTheRoot;
            }
            """,
            libraries:
            [
                ("Ergosfare.SiteLibrary", """
                    using Stella.Ergosfare.Core.Abstractions.DispatchSites;
                    using Stella.Ergosfare.Commands.Abstractions;

                    [assembly: DispatchManifest(1)]
                    [assembly: DispatchSite("TestLib.LonelyCommand", DispatchKind.Command, false)]

                    namespace TestLib
                    {
                        public sealed record LonelyCommand : ICommand;
                    }
                    """),
            ],
            buildProperties: CompositionRoot);

        Assert.Empty(result.CompilationErrors);

        var diagnostic = Assert.Single(result.GeneratorDiagnostics);
        Assert.Equal("ERGOSG005", diagnostic.Id);
        Assert.Equal(Location.None, diagnostic.Location);
        Assert.Contains("LonelyCommand", diagnostic.GetMessage());
        Assert.Contains("Ergosfare.SiteLibrary", diagnostic.GetMessage());
    }

    [Fact]
    public void ExclusionChangesNothing_AnUnregisteredDispatchStillFails()
    {
        var result = GeneratorTestHost.Run("""
            using System.Threading.Tasks;
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Attributes;

            namespace TestApp
            {
                [ExcludeFromDiscovery]
                public sealed record RuntimeOnlyCommand : ICommand;

                public class Caller(ICommandMediator mediator)
                {
                    public ValueTask Fire(RuntimeOnlyCommand command) => mediator.SendAsync(command);
                }
            }
            """,
            buildProperties: CompositionRoot);

        Assert.Empty(result.CompilationErrors);

        // [ExcludeFromDiscovery] only hides a type from bulk collection; it does not
        // register anything. No collection path feeds this message a handler → dead.
        var diagnostic = Assert.Single(result.GeneratorDiagnostics);
        Assert.Equal("ERGOSG005", diagnostic.Id);
    }

    [Fact]
    public void ManualRegistration_IsCoverageEvidence_ForAnExcludedHandler()
    {
        const string sourceWithoutRegistration = """
            using System.Threading.Tasks;
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Attributes;

            namespace TestApp
            {
                public sealed record Ping : ICommand;

                [ExcludeFromDiscovery]
                public sealed class ManualPingHandler : ICommandHandler<Ping>
                {
                    public ValueTask HandleAsync(Ping message, Stella.Ergosfare.Core.Abstractions.ErgosfareContext context)
                        => default;
                }

                public class Caller(ICommandMediator mediator)
                {
                    public ValueTask Fire(Ping ping) => mediator.SendAsync(ping);
                }
            }
            """;

        var uncovered = GeneratorTestHost.Run(sourceWithoutRegistration, buildProperties: CompositionRoot);

        Assert.Empty(uncovered.CompilationErrors);
        Assert.Equal("ERGOSG005", Assert.Single(uncovered.GeneratorDiagnostics).Id);

        // Register<T>() is the same collection path as RegisterGenerated(), per type
        // instead of in bulk — a visible call is coverage evidence.
        var registered = GeneratorTestHost.Run(sourceWithoutRegistration.Replace(
                "public class Caller(ICommandMediator mediator)",
                """
                public static class Boot
                {
                    public static void Configure(Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection.CommandModuleBuilder commands)
                        => commands.Register<ManualPingHandler>();
                }

                public class Caller(ICommandMediator mediator)
                """),
            buildProperties: CompositionRoot);

        Assert.Empty(registered.CompilationErrors);
        Assert.Empty(registered.GeneratorDiagnostics);
    }

    [Fact]
    public void ExcludedSubtype_NeedsARegistrationToSaveTheBaseTypedDispatch()
    {
        const string source = """
            using System.Threading.Tasks;
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Attributes;

            namespace TestApp
            {
                public abstract record StarCommand : ICommand;

                [ExcludeFromDiscovery]
                public sealed record RuntimeMagnetarCommand : StarCommand;

                [ExcludeFromDiscovery]
                public sealed class RuntimeMagnetarHandler : ICommandHandler<RuntimeMagnetarCommand>
                {
                    public ValueTask HandleAsync(RuntimeMagnetarCommand message, Stella.Ergosfare.Core.Abstractions.ErgosfareContext context)
                        => default;
                }

                public class Caller(ICommandMediator mediator)
                {
                    public ValueTask Fire(StarCommand command) => mediator.SendAsync(command);
                }
            }
            """;

        var uncovered = GeneratorTestHost.Run(source, buildProperties: CompositionRoot);

        Assert.Empty(uncovered.CompilationErrors);

        // The excluded subtype exists, but nothing collects its handler: every possible
        // runtime instance of the dispatch is uncovered.
        Assert.Equal("ERGOSG005", Assert.Single(uncovered.GeneratorDiagnostics).Id);

        var registered = GeneratorTestHost.Run(source.Replace(
                "public class Caller(ICommandMediator mediator)",
                """
                public static class Boot
                {
                    public static void Configure(Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection.CommandModuleBuilder commands)
                        => commands.Register<RuntimeMagnetarHandler>();
                }

                public class Caller(ICommandMediator mediator)
                """),
            buildProperties: CompositionRoot);

        Assert.Empty(registered.CompilationErrors);
        Assert.Empty(registered.GeneratorDiagnostics);
    }

    [Fact]
    public void OpaqueRegistration_SuspendsDeadDispatchVerdicts()
    {
        var result = GeneratorTestHost.Run("""
            using System;
            using System.Threading.Tasks;
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;

            namespace TestApp
            {
                public sealed record OrphanCommand : ICommand;

                public static class Boot
                {
                    public static void Configure(CommandModuleBuilder commands, Type runtimeType)
                        => commands.Register(runtimeType);
                }

                public class Caller(ICommandMediator mediator)
                {
                    public ValueTask Fire(OrphanCommand command) => mediator.SendAsync(command);
                }
            }
            """,
            buildProperties: CompositionRoot);

        Assert.Empty(result.CompilationErrors);

        // A runtime-computed Register argument is unknowable at compile time: coverage
        // evidence is incomplete by construction, so no dead-dispatch verdict is sound.
        Assert.Empty(result.GeneratorDiagnostics);
    }

    [Fact]
    public void ManualRegistrationRecordedInAReferencedManifest_CoversTheRootDispatch()
    {
        var result = GeneratorTestHost.Run("""
            using System.Threading.Tasks;
            using Stella.Ergosfare.Commands.Abstractions;

            namespace TestApp
            {
                public class Caller(ICommandMediator mediator)
                {
                    public ValueTask Fire(TestLib.LibCommand command) => mediator.SendAsync(command);
                }
            }
            """,
            libraries:
            [
                ("Ergosfare.RegistrationLibrary", """
                    using System.Threading.Tasks;
                    using Stella.Ergosfare.Commands.Abstractions;
                    using Stella.Ergosfare.Core.Abstractions.Attributes;
                    using Stella.Ergosfare.Core.Abstractions.DispatchSites;

                    [assembly: DispatchManifest(1)]
                    [assembly: ManualRegistration("TestLib.LibCommandHandler")]

                    namespace TestLib
                    {
                        public sealed record LibCommand : ICommand;

                        [ExcludeFromDiscovery]
                        public sealed class LibCommandHandler : ICommandHandler<LibCommand>
                        {
                            public ValueTask HandleAsync(LibCommand message, Stella.Ergosfare.Core.Abstractions.ErgosfareContext context)
                                => default;
                        }
                    }
                    """),
            ],
            buildProperties: CompositionRoot);

        Assert.Empty(result.CompilationErrors);

        // The library's manifest says it registers the handler itself; the root's
        // dispatch of the library message is covered by that evidence.
        Assert.Empty(result.GeneratorDiagnostics);
    }

    [Fact]
    public void OpaqueRegistrationFlagInAReferencedManifest_SuspendsRootVerdicts()
    {
        var result = GeneratorTestHost.Run("""
            using System.Threading.Tasks;
            using Stella.Ergosfare.Commands.Abstractions;

            namespace TestApp
            {
                public sealed record OrphanCommand : ICommand;

                public class Caller(ICommandMediator mediator)
                {
                    public ValueTask Fire(OrphanCommand command) => mediator.SendAsync(command);
                }
            }
            """,
            libraries:
            [
                ("Ergosfare.OpaqueLibrary", """
                    using Stella.Ergosfare.Core.Abstractions.DispatchSites;

                    [assembly: DispatchManifest(1, HasOpaqueRegistrations = true)]

                    namespace TestLib
                    {
                        public sealed class Marker;
                    }
                    """),
            ],
            buildProperties: CompositionRoot);

        Assert.Empty(result.CompilationErrors);

        // Some assembly in the closure registers types the compiler cannot see; no
        // dead-dispatch claim is sound anywhere.
        Assert.Empty(result.GeneratorDiagnostics);
    }

    [Fact]
    public void OpaqueSite_IsSilentByDefault_AndReportsWhenRaised()
    {
        const string source = """
            using System.Threading.Tasks;
            using Stella.Ergosfare.Commands.Abstractions;

            namespace TestApp
            {
                public sealed record Ping : ICommand;

                public sealed class PingHandler : ICommandHandler<Ping>
                {
                    public ValueTask HandleAsync(Ping message, Stella.Ergosfare.Core.Abstractions.ErgosfareContext context)
                        => default;
                }

                public class Caller(ICommandMediator mediator)
                {
                    public ValueTask Fire(ICommand whatever) => mediator.SendAsync(whatever);
                }
            }
            """;

        var silent = GeneratorTestHost.Run(source, buildProperties: CompositionRoot);

        Assert.Empty(silent.CompilationErrors);
        Assert.Empty(silent.GeneratorDiagnostics);

        var raised = GeneratorTestHost.Run(source, buildProperties: CompositionRoot,
            diagnosticOptions: new Dictionary<string, Microsoft.CodeAnalysis.ReportDiagnostic>
            {
                ["ERGOSG009"] = Microsoft.CodeAnalysis.ReportDiagnostic.Warn,
            });

        Assert.Empty(raised.CompilationErrors);

        var diagnostic = Assert.Single(raised.GeneratorDiagnostics);
        Assert.Equal("ERGOSG009", diagnostic.Id);
        Assert.Contains("ICommand", diagnostic.GetMessage());
    }
}
