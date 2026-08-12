using Microsoft.CodeAnalysis;

namespace Stella.Ergosfare.SourceGenerator.Test;

/// <summary>
///     Unreachable-handler verdicts (ERGOSG007) and the opt-in trim (ERGOSG008): a handler
///     no dispatch site in the closure can deliver to warns at the composition root — but
///     only while the closure's dispatch manifests are complete, and never for
///     keyed-discovery types. The trim replaces the warning by excluding pure main-handler
///     types from generated registration entirely.
/// </summary>
public class UnreachableHandlerDiagnosticTests
{
    private static readonly IReadOnlyDictionary<string, string> CompositionRoot =
        new Dictionary<string, string> { ["ErgosfareCompositionRoot"] = "true" };

    private static readonly IReadOnlyDictionary<string, string> CompositionRootWithTrim =
        new Dictionary<string, string>
        {
            ["ErgosfareCompositionRoot"] = "true",
            ["ErgosfareTrimUnusedHandlers"] = "true",
        };

    private const string HandlerWithoutAnySite = """
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
        }
        """;

    [Fact]
    public void HandlerWithoutAnyDispatchSite_Warns()
    {
        var result = GeneratorTestHost.Run(HandlerWithoutAnySite, buildProperties: CompositionRoot);

        Assert.Empty(result.CompilationErrors);

        var diagnostic = Assert.Single(result.GeneratorDiagnostics);
        Assert.Equal("ERGOSG007", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("PingHandler", diagnostic.GetMessage());
        Assert.Contains("TestApp.Ping", diagnostic.GetMessage());
        Assert.NotEqual(Location.None, diagnostic.Location);
    }

    [Fact]
    public void DispatchSite_MakesTheHandlerReachable()
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
    public void OpaqueMarkerSite_ReachesEveryHandlerOfTheKind()
    {
        var result = GeneratorTestHost.Run("""
            using System.Threading.Tasks;
            using Stella.Ergosfare.Commands.Abstractions;

            namespace TestApp
            {
                public sealed record Ping : ICommand;

                public sealed record Pong : ICommand;

                public sealed class PingHandler : ICommandHandler<Ping>
                {
                    public ValueTask HandleAsync(Ping message, Stella.Ergosfare.Core.Abstractions.ErgosfareContext context)
                        => default;
                }

                public sealed class PongHandler : ICommandHandler<Pong>
                {
                    public ValueTask HandleAsync(Pong message, Stella.Ergosfare.Core.Abstractions.ErgosfareContext context)
                        => default;
                }

                public class Caller(ICommandMediator mediator)
                {
                    public ValueTask Fire(ICommand whatever) => mediator.SendAsync(whatever);
                }
            }
            """,
            buildProperties: CompositionRoot);

        Assert.Empty(result.CompilationErrors);

        // The marker-typed site could carry any command in the closure; both handlers
        // stay conservatively reachable.
        Assert.Empty(result.GeneratorDiagnostics);
    }

    [Fact]
    public void BaseTypedSite_ReachesTheSubtypeHandler()
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
        Assert.Empty(result.GeneratorDiagnostics);
    }

    [Fact]
    public void KeyedDiscoveryHandler_IsExemptFromTheJudgment()
    {
        var result = GeneratorTestHost.Run("""
            using System.Threading.Tasks;
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Attributes;

            namespace TestApp
            {
                public sealed record Ping : ICommand;

                [DiscoveryKey("reporting")]
                public sealed class PingHandler : ICommandHandler<Ping>
                {
                    public ValueTask HandleAsync(Ping message, Stella.Ergosfare.Core.Abstractions.ErgosfareContext context)
                        => default;
                }
            }
            """,
            buildProperties: CompositionRoot);

        Assert.Empty(result.CompilationErrors);

        // Keyed participation is deliberately conditional; reachability says nothing
        // about whether the key is ever selected.
        Assert.Empty(result.GeneratorDiagnostics);
    }

    [Fact]
    public void ManifestlessReferencedAssembly_SuspendsTheJudgment()
    {
        var result = GeneratorTestHost.Run(HandlerWithoutAnySite,
            libraries:
            [
                ("Ergosfare.LegacyLibrary", """
                    using Stella.Ergosfare.Commands.Abstractions;

                    namespace TestLib
                    {
                        public sealed record LegacyCommand : ICommand;
                    }
                    """),
            ],
            buildProperties: CompositionRoot);

        Assert.Empty(result.CompilationErrors);

        // The legacy library carries no dispatch manifest: its sites are unknowable, so
        // no handler can soundly be called unreachable.
        Assert.Empty(result.GeneratorDiagnostics);
    }

    [Fact]
    public void ManifestSiteInAReferencedAssembly_ReachesTheRootHandler()
    {
        var result = GeneratorTestHost.Run(HandlerWithoutAnySite,
            libraries:
            [
                ("Ergosfare.SiteLibrary", """
                    using Stella.Ergosfare.Core.Abstractions.DispatchSites;

                    [assembly: DispatchManifest(1)]
                    [assembly: DispatchSite("TestApp.Ping", DispatchKind.Command, false)]

                    namespace TestLib
                    {
                        public sealed class Marker;
                    }
                    """),
            ],
            buildProperties: CompositionRoot);

        Assert.Empty(result.CompilationErrors);
        Assert.Empty(result.GeneratorDiagnostics);
    }

    [Fact]
    public void ExcludedSubtypePresence_DoesNotShieldAnUnreachedHandler()
    {
        var result = GeneratorTestHost.Run("""
            using System.Threading.Tasks;
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Attributes;

            namespace TestApp
            {
                public abstract record StarCommand : ICommand;

                [ExcludeFromDiscovery]
                public sealed record RuntimeMagnetarCommand : StarCommand;

                public sealed class StarHandler : ICommandHandler<StarCommand>
                {
                    public ValueTask HandleAsync(StarCommand message, Stella.Ergosfare.Core.Abstractions.ErgosfareContext context)
                        => default;
                }
            }
            """,
            buildProperties: CompositionRoot);

        Assert.Empty(result.CompilationErrors);

        // Excluded types are hidden from bulk collection, not from the site scan: their
        // dispatches are recorded like any other. With zero sites anywhere, the handler
        // is unreached — a warning, never an error.
        var diagnostic = Assert.Single(result.GeneratorDiagnostics);
        Assert.Equal("ERGOSG007", diagnostic.Id);
        Assert.Contains("StarHandler", diagnostic.GetMessage());
    }

    [Fact]
    public void ADispatchOfTheExcludedSubtype_ReachesTheBaseHandler()
    {
        var result = GeneratorTestHost.Run("""
            using System.Threading.Tasks;
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Attributes;

            namespace TestApp
            {
                public abstract record StarCommand : ICommand;

                [ExcludeFromDiscovery]
                public sealed record RuntimeMagnetarCommand : StarCommand;

                public sealed class StarHandler : ICommandHandler<StarCommand>
                {
                    public ValueTask HandleAsync(StarCommand message, Stella.Ergosfare.Core.Abstractions.ErgosfareContext context)
                        => default;
                }

                public class Caller(ICommandMediator mediator)
                {
                    public ValueTask Fire(RuntimeMagnetarCommand command) => mediator.SendAsync(command);
                }
            }
            """,
            buildProperties: CompositionRoot);

        Assert.Empty(result.CompilationErrors);

        // The excluded subtype's dispatch is an ordinary site; the covariant base
        // registration covers it (no 005) and is reached by it (no 007).
        Assert.Empty(result.GeneratorDiagnostics);
    }

    [Fact]
    public void UnusedEventHandler_WarnsToo()
    {
        var result = GeneratorTestHost.Run("""
            using System.Threading.Tasks;
            using Stella.Ergosfare.Events.Abstractions;

            namespace TestApp
            {
                public sealed record Blinked : IEvent;

                public sealed class BlinkedHandler : IEventHandler<Blinked>
                {
                    public ValueTask HandleAsync(Blinked message, Stella.Ergosfare.Core.Abstractions.ErgosfareContext context)
                        => default;
                }
            }
            """,
            buildProperties: CompositionRoot);

        Assert.Empty(result.CompilationErrors);

        var diagnostic = Assert.Single(result.GeneratorDiagnostics);
        Assert.Equal("ERGOSG007", diagnostic.Id);
        Assert.Contains("BlinkedHandler", diagnostic.GetMessage());
    }

    [Fact]
    public void Trim_ExcludesTheUnreachableHandlerAndReportsErgosg008()
    {
        var result = GeneratorTestHost.Run(HandlerWithoutAnySite, buildProperties: CompositionRootWithTrim);

        Assert.Empty(result.CompilationErrors);

        var diagnostic = Assert.Single(result.GeneratorDiagnostics);
        Assert.Equal("ERGOSG008", diagnostic.Id);
        Assert.Contains("PingHandler", diagnostic.GetMessage());

        // The handler is gone from every emitted surface; the plain message remains.
        Assert.DoesNotContain("PingHandler", result.GeneratedSource);
        Assert.Contains("typeof(global::TestApp.Ping)", result.GeneratedSource);
    }

    [Fact]
    public void Trim_KeepsReachableHandlers()
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
            buildProperties: CompositionRootWithTrim);

        Assert.Empty(result.CompilationErrors);
        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Contains("PingHandler", result.GeneratedSource);
    }

    [Fact]
    public void Trim_SparesTypesThatAlsoCarryInterceptorContracts()
    {
        var result = GeneratorTestHost.Run("""
            using System.Threading.Tasks;
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Handlers;

            namespace TestApp
            {
                public sealed record Ping : ICommand;

                public sealed record Pong : ICommand;

                public sealed class PongHandler : ICommandHandler<Pong>
                {
                    public ValueTask HandleAsync(Pong message, ErgosfareContext context)
                        => default;
                }

                public sealed class DualRole :
                    ICommandHandler<Ping>,
                    IAsyncPreInterceptor<Pong>
                {
                    public ValueTask HandleAsync(Ping message, ErgosfareContext context)
                        => default;

                    public ValueTask<object> HandleAsync(Pong message, ErgosfareContext context)
                        => new((object)message);
                }

                public class Caller(ICommandMediator mediator)
                {
                    public ValueTask Fire(Pong pong) => mediator.SendAsync(pong);
                }
            }
            """,
            buildProperties: CompositionRootWithTrim);

        Assert.Empty(result.CompilationErrors);

        // Pong's dispatch is dead-handler-free (DualRole handles only Ping), so the type's
        // main-handler role is unreachable — but its interceptor role participates in
        // Pong's pipeline, so it must stay registered and the warning stays a warning.
        var diagnostic = Assert.Single(result.GeneratorDiagnostics, d => d.Id == "ERGOSG007");
        Assert.Contains("DualRole", diagnostic.GetMessage());
        Assert.Contains("DualRole", result.GeneratedSource);
    }
}
