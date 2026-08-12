using Microsoft.CodeAnalysis;

namespace Stella.Ergosfare.SourceGenerator.Test;

/// <summary>
/// ERGOSG010: a provable same-level main-handler contest — several direct claimants, or
/// several covariant ones with provably no direct winner — fails the build, mirroring the
/// runtime priority ladder (a direct handler beats covariant ones; within a level there is
/// no tiebreaker). Keyed or grouped registrations are container choices and abstain, so
/// deliberately contested keyed suites keep compiling; the covariant arm suspends whenever
/// a direct handler could exist unseen.
/// </summary>
public class ContestedMainHandlerDiagnosticTests
{
    private static readonly Dictionary<string, string> CompositionRoot =
        new() { ["ErgosfareCompositionRoot"] = "true" };

    private const string Preamble = """
        using System.Threading.Tasks;
        using Stella.Ergosfare.Commands.Abstractions;
        using Stella.Ergosfare.Core.Abstractions;
        using Stella.Ergosfare.Core.Abstractions.Attributes;

        namespace TestApp
        {
        """;

    private static void AssertSingle010(GeneratorTestHost.GeneratorRunResult result, string levelWord)
    {
        var diagnostic = Assert.Single(result.GeneratorDiagnostics, d => d.Id == "ERGOSG010");
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains(levelWord, diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void TwoDirectHandlers_FailTheBuild()
    {
        var result = GeneratorTestHost.Run(Preamble + """

            public sealed record Contested : ICommand;

            public sealed class FirstHandler : ICommandHandler<Contested>
            {
                public ValueTask HandleAsync(Contested message, ErgosfareContext context) => default;
            }

            public sealed class SecondHandler : ICommandHandler<Contested>
            {
                public ValueTask HandleAsync(Contested message, ErgosfareContext context) => default;
            }
        }
        """, buildProperties: CompositionRoot);

        AssertSingle010(result, "direct");
    }

    [Fact]
    public void DirectPlusCovariant_IsNotAContest()
    {
        var result = GeneratorTestHost.Run(Preamble + """

            public abstract record EntryBase : ICommand;

            public sealed record Entry : EntryBase;

            public sealed class EntryHandler : ICommandHandler<Entry>
            {
                public ValueTask HandleAsync(Entry message, ErgosfareContext context) => default;
            }

            public sealed class EntryBaseHandler : ICommandHandler<EntryBase>
            {
                public ValueTask HandleAsync(EntryBase message, ErgosfareContext context) => default;
            }
        }
        """, buildProperties: CompositionRoot);

        // The direct level wins outright at runtime, so this shape is legal — the base
        // handler is Entry's fallback and EntryBase's own direct handler.
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id == "ERGOSG010");
    }

    [Fact]
    public void TwoCovariantClaimantsWithNoDirectHandler_FailTheBuild()
    {
        var result = GeneratorTestHost.Run(Preamble + """

            public interface IArchived : ICommand;

            public abstract record EntryBase : ICommand;

            public sealed record ArchivedEntry : EntryBase, IArchived;

            public sealed class EntryBaseHandler : ICommandHandler<EntryBase>
            {
                public ValueTask HandleAsync(EntryBase message, ErgosfareContext context) => default;
            }

            public sealed class ArchivedHandler : ICommandHandler<IArchived>
            {
                public ValueTask HandleAsync(IArchived message, ErgosfareContext context) => default;
            }
        }
        """, buildProperties: CompositionRoot);

        AssertSingle010(result, "covariant");
    }

    [Fact]
    public void KeyedClaimants_Abstain()
    {
        var result = GeneratorTestHost.Run(Preamble + """

            public sealed record Contested : ICommand;

            [DiscoveryKey("a")]
            public sealed class FirstHandler : ICommandHandler<Contested>
            {
                public ValueTask HandleAsync(Contested message, ErgosfareContext context) => default;
            }

            [DiscoveryKey("b")]
            public sealed class SecondHandler : ICommandHandler<Contested>
            {
                public ValueTask HandleAsync(Contested message, ErgosfareContext context) => default;
            }
        }
        """, buildProperties: CompositionRoot);

        // Which keys a container registers is a runtime choice; nothing is provable here.
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id == "ERGOSG010");
    }

    [Fact]
    public void ALiteralDirectRegistration_SuspendsTheCovariantArm()
    {
        var result = GeneratorTestHost.Run(Preamble + """

            public interface IArchived : ICommand;

            public abstract record EntryBase : ICommand;

            public sealed record ArchivedEntry : EntryBase, IArchived;

            public sealed class EntryBaseHandler : ICommandHandler<EntryBase>
            {
                public ValueTask HandleAsync(EntryBase message, ErgosfareContext context) => default;
            }

            public sealed class ArchivedHandler : ICommandHandler<IArchived>
            {
                public ValueTask HandleAsync(IArchived message, ErgosfareContext context) => default;
            }

            // Hidden from discovery, registered literally: provable direct-claim evidence
            // for ArchivedEntry — the covariant level no longer decides, so no contest.
            [ExcludeFromDiscovery]
            public sealed class ArchivedEntryHandler : ICommandHandler<ArchivedEntry>
            {
                public ValueTask HandleAsync(ArchivedEntry message, ErgosfareContext context) => default;
            }

            public static class Boot
            {
                public static void Register(global::Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection.CommandModuleBuilder commands)
                    => commands.Register<ArchivedEntryHandler>();
            }
        }
        """, buildProperties: CompositionRoot);

        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id == "ERGOSG010");
    }

    [Fact]
    public void OutsideTheCompositionRoot_NoJudgmentRuns()
    {
        var result = GeneratorTestHost.Run(Preamble + """

            public sealed record Contested : ICommand;

            public sealed class FirstHandler : ICommandHandler<Contested>
            {
                public ValueTask HandleAsync(Contested message, ErgosfareContext context) => default;
            }

            public sealed class SecondHandler : ICommandHandler<Contested>
            {
                public ValueTask HandleAsync(Contested message, ErgosfareContext context) => default;
            }
        }
        """);

        // A library cannot know the closure; the verdict belongs to the root.
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id == "ERGOSG010");
    }
}
