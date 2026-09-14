using Microsoft.CodeAnalysis;

namespace Stella.Ergosfare.SourceGenerator.Test;

/// <summary>
///     The mirror of ERGO023: a dispatch whose group filter provably selects no main handler
///     (ERGO024). The message is covered — some handler claims it — and the set the call names
///     selects none of them, so every dispatch lane throws. Judged behind the
///     same gates as ERGO005, because a registration this compilation cannot read could be the
///     one supplying the missing group.
/// </summary>
public class NoHandlerInGroupSetDiagnosticTests
{
    private static readonly IReadOnlyDictionary<string, string> CompositionRoot =
        new Dictionary<string, string> { ["ErgosfareCompositionRoot"] = "true" };

    /// <summary>
    ///     A command whose only handler runs in the <c>Admin</c> group. Callers are appended
    ///     inside the namespace, so each test names its own dispatch against one pipeline.
    /// </summary>
    private const string GroupedCommandPipeline = """
        using System.Threading.Tasks;
        using Stella.Ergosfare.Commands.Abstractions;
        using Stella.Ergosfare.Core.Abstractions;
        using Stella.Ergosfare.Core.Abstractions.Attributes;

        namespace TestApp
        {
            public sealed record LoginCommand : ICommand;

            [Group("Admin")]
            public sealed class AdminLoginHandler : ICommandHandler<LoginCommand>
            {
                public ValueTask HandleAsync(LoginCommand message, ErgosfareContext context) => default;
            }
        """;

    [Fact]
    [Trait("Category", "Unit")]
    public void UngroupedSend_WhenEveryHandlerIsGrouped_FailsTheBuild()
    {
        var result = GeneratorTestHost.RunWithAllCandidates(GroupedCommandPipeline + """

            public class Caller(ICommandMediator mediator)
            {
                public ValueTask Fire(LoginCommand command) => mediator.SendAsync(command);
            }
        }
        """,
            buildProperties: CompositionRoot);

        Assert.Empty(result.CompilationErrors);

        var diagnostic = Assert.Single(result.GeneratorDiagnostics);
        Assert.Equal("ERGO024", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.NotEqual(Location.None, diagnostic.Location);

        var message = diagnostic.GetMessage();
        Assert.Contains("LoginCommand", message);
        Assert.Contains("no group set", message);
        Assert.Contains("AdminLoginHandler", message);

        // The handler's own groups are named, because the fix is a change to one of the two
        // lists and neither is visible from the other file.
        Assert.Contains("[Admin]", message);
        Assert.Contains("NoHandlerFoundException", message);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void UngroupedQuery_WhenEveryHandlerIsGrouped_FailsTheBuild()
    {
        var result = GeneratorTestHost.RunWithAllCandidates("""
            using System.Threading.Tasks;
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Attributes;
            using Stella.Ergosfare.Queries.Abstractions;

            namespace TestApp
            {
                public sealed record TotalQuery : IQuery<int>;

                [Group("Reporting")]
                public sealed class TotalHandler : IQueryHandler<TotalQuery, int>
                {
                    public ValueTask<int> HandleAsync(TotalQuery message, ErgosfareContext context) => new(1);
                }

                public class Caller(IQueryMediator mediator)
                {
                    public ValueTask<int> Ask(TotalQuery query) => mediator.QueryAsync(query);
                }
            }
            """,
            buildProperties: CompositionRoot);

        Assert.Empty(result.CompilationErrors);

        var diagnostic = Assert.Single(result.GeneratorDiagnostics);
        Assert.Equal("ERGO024", diagnostic.Id);
        Assert.Contains("TotalQuery", diagnostic.GetMessage());
        Assert.Contains("[Reporting]", diagnostic.GetMessage());
    }

    /// <summary>
    ///     A statically known event group without handlers is a compile error.
    /// </summary>
    [Theory]
    [InlineData("e")]
    [InlineData("e, [\"Nobody\"], System.Threading.CancellationToken.None")]
    [Trait("Category", "Unit")]
    public void PublishWithoutSelectedHandlers_FailsTheBuild(string arguments)
    {
        var result = GeneratorTestHost.RunWithAllCandidates("""
            using System.Threading.Tasks;
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Attributes;
            using Stella.Ergosfare.Events.Abstractions;

            namespace TestApp
            {
                public sealed record OrderPlaced : IEvent;

                [Group("Audit")]
                public sealed class AuditSubscriber : IEventHandler<OrderPlaced>
                {
                    public ValueTask HandleAsync(OrderPlaced message, ErgosfareContext context) => default;
                }

                public class Caller(IEventMediator mediator)
                {
                    public ValueTask Fire(OrderPlaced e) => mediator.PublishAsync(ARGUMENTS);
                }
            }
            """.Replace("ARGUMENTS", arguments),
            buildProperties: CompositionRoot);

        Assert.Empty(result.CompilationErrors);

        var diagnostic = Assert.Single(result.GeneratorDiagnostics);
        Assert.Equal("ERGO024", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);

        Assert.Contains("NoHandlerFoundException", diagnostic.GetMessage());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ALiteralGroupSetMatchingNobody_FailsTheBuild()
    {
        var result = GeneratorTestHost.RunWithAllCandidates(GroupedCommandPipeline + """

            public class Caller(ICommandMediator mediator)
            {
                public ValueTask Fire(LoginCommand command) => mediator.SendAsync(command, ["User"]);
            }
        }
        """,
            buildProperties: CompositionRoot);

        Assert.Empty(result.CompilationErrors);

        var diagnostic = Assert.Single(result.GeneratorDiagnostics);
        Assert.Equal("ERGO024", diagnostic.Id);

        var message = diagnostic.GetMessage();
        Assert.Contains("group set [User]", message);
        Assert.Contains("AdminLoginHandler", message);
        Assert.Contains("[Admin]", message);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ALiteralGroupSetMatchingAHandler_IsSilent()
    {
        var result = GeneratorTestHost.RunWithAllCandidates(GroupedCommandPipeline + """

            public class Caller(ICommandMediator mediator)
            {
                public ValueTask Fire(LoginCommand command)
                    => mediator.SendAsync(command, ["Admin"]);
            }
        }
        """,
            buildProperties: CompositionRoot);

        Assert.Empty(result.CompilationErrors);
        Assert.Empty(result.GeneratorDiagnostics);
    }

    /// <summary>
    ///     A handler declaring no groups is in the default group, so it serves the dispatch
    ///     that names none — whatever its grouped siblings do.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void AnUngroupedHandlerBesideGroupedOnes_KeepsTheDefaultDispatchAlive()
    {
        var result = GeneratorTestHost.RunWithAllCandidates("""
            using System.Threading.Tasks;
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Attributes;
            using Stella.Ergosfare.Events.Abstractions;

            namespace TestApp
            {
                public sealed record OrderPlaced : IEvent;

                [Group("Audit")]
                public sealed class AuditSubscriber : IEventHandler<OrderPlaced>
                {
                    public ValueTask HandleAsync(OrderPlaced message, ErgosfareContext context) => default;
                }

                public sealed class NotifySubscriber : IEventHandler<OrderPlaced>
                {
                    public ValueTask HandleAsync(OrderPlaced message, ErgosfareContext context) => default;
                }

                public class Caller(IEventMediator mediator)
                {
                    public ValueTask Fire(OrderPlaced e) => mediator.PublishAsync(e);
                }
            }
            """,
            buildProperties: CompositionRoot);

        Assert.Empty(result.CompilationErrors);
        Assert.Empty(result.GeneratorDiagnostics);
    }

    /// <summary>
    ///     A handler may name the default group outright, which is the same membership an
    ///     undecorated one has.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void AHandlerDeclaringTheDefaultGroup_ServesTheUngroupedDispatch()
    {
        var result = GeneratorTestHost.RunWithAllCandidates("""
            using System.Threading.Tasks;
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Attributes;

            namespace TestApp
            {
                public sealed record LoginCommand : ICommand;

                [Group("default")]
                public sealed class LoginHandler : ICommandHandler<LoginCommand>
                {
                    public ValueTask HandleAsync(LoginCommand message, ErgosfareContext context) => default;
                }

                public class Caller(ICommandMediator mediator)
                {
                    public ValueTask Fire(LoginCommand command) => mediator.SendAsync(command);
                }
            }
            """,
            buildProperties: CompositionRoot);

        Assert.Empty(result.CompilationErrors);
        Assert.Empty(result.GeneratorDiagnostics);
    }

    /// <summary>
    ///     A base-typed handler serves the derived message covariantly, and its groups decide
    ///     the same way a direct handler's do — so a matching set is coverage.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void ACovariantHandlerInTheSet_KeepsTheDispatchAlive()
    {
        const string source = """
            using System.Threading.Tasks;
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Attributes;

            namespace TestApp
            {
                public abstract record StarCommand : ICommand;

                public sealed record MagnetarCommand : StarCommand;

                [Group("Admin")]
                public sealed class StarHandler : ICommandHandler<StarCommand>
                {
                    public ValueTask HandleAsync(StarCommand message, ErgosfareContext context) => default;
                }

                public class Caller(ICommandMediator mediator)
                {
                    public ValueTask Fire(MagnetarCommand command) => mediator.SendAsync(command, GROUPS);
                }
            }
            """;

        var matching = GeneratorTestHost.RunWithAllCandidates(
            source.Replace("GROUPS", """["Admin"]"""), buildProperties: CompositionRoot);

        Assert.Empty(matching.CompilationErrors);
        Assert.Empty(matching.GeneratorDiagnostics);

        // The same covariant reach, under a set the handler is not in: coverage exists and
        // the filter still empties it.
        var missing = GeneratorTestHost.RunWithAllCandidates(
            source.Replace("GROUPS", """["User"]"""), buildProperties: CompositionRoot);

        Assert.Empty(missing.CompilationErrors);

        var diagnostic = Assert.Single(missing.GeneratorDiagnostics);
        Assert.Equal("ERGO024", diagnostic.Id);
        Assert.Contains("StarHandler", diagnostic.GetMessage());
    }

    /// <summary>
    ///     A base-typed dispatch is alive when any runtime instance it could carry has a
    ///     handler in the set, even where the static type's own handler is not.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void ASubtypeHandlerInTheSet_KeepsTheBaseTypedDispatchAlive()
    {
        var result = GeneratorTestHost.RunWithAllCandidates("""
            using System.Threading.Tasks;
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Attributes;

            namespace TestApp
            {
                public record StarCommand : ICommand;

                public sealed record MagnetarCommand : StarCommand;

                [Group("Admin")]
                public sealed class StarHandler : ICommandHandler<StarCommand>
                {
                    public ValueTask HandleAsync(StarCommand message, ErgosfareContext context) => default;
                }

                public sealed class MagnetarHandler : ICommandHandler<MagnetarCommand>
                {
                    public ValueTask HandleAsync(MagnetarCommand message, ErgosfareContext context) => default;
                }

                public class Caller(ICommandMediator mediator)
                {
                    public ValueTask Fire(StarCommand command) => mediator.SendAsync(command);
                }
            }
            """,
            buildProperties: CompositionRoot);

        Assert.Empty(result.CompilationErrors);

        // MagnetarCommand's ungrouped handler serves the default set, and the dispatch can
        // carry one — so the ungrouped send is not provably dead.
        Assert.Empty(result.GeneratorDiagnostics);
    }

    /// <summary>
    ///     A set the compilation cannot fold to names says nothing about who it selects; the
    ///     filtering plan answers it at run time instead.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void ARuntimeValuedGroupSet_IsNotJudged()
    {
        var result = GeneratorTestHost.RunWithAllCandidates(GroupedCommandPipeline + """

            public class Caller(ICommandMediator mediator)
            {
                public ValueTask Fire(LoginCommand command, Stella.Ergosfare.Core.Abstractions.GroupSet groups)
                    => mediator.SendAsync(command, groups);
            }
        }
        """,
            buildProperties: CompositionRoot);

        Assert.Empty(result.CompilationErrors);
        Assert.Empty(result.GeneratorDiagnostics);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void NonRootCompilation_SuspendsTheVerdict()
    {
        var result = GeneratorTestHost.RunWithAllCandidates(GroupedCommandPipeline + """

            public class Caller(ICommandMediator mediator)
            {
                public ValueTask Fire(LoginCommand command) => mediator.SendAsync(command);
            }
        }
        """);

        Assert.Empty(result.CompilationErrors);

        // A library's closure is not the application's; the missing group could be declared
        // by a handler in a compilation that has not happened yet.
        Assert.Empty(result.GeneratorDiagnostics);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void OpaqueRegistration_DoesNotDisableGroupValidation()
    {
        var result = GeneratorTestHost.RunWithAllCandidates("""
            using System;
            using System.Threading.Tasks;
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Attributes;

            namespace TestApp
            {
                public sealed record LoginCommand : ICommand;

                [Group("Admin")]
                public sealed class AdminLoginHandler : ICommandHandler<LoginCommand>
                {
                    public ValueTask HandleAsync(LoginCommand message, ErgosfareContext context) => default;
                }

                public static class Boot
                {
                    public static void Configure(CommandModuleBuilder commands, Type runtimeType)
                        => commands.Register(runtimeType);
                }

                public class Caller(ICommandMediator mediator)
                {
                    public ValueTask Fire(LoginCommand command) => mediator.SendAsync(command);
                }
            }
            """,
            buildProperties: CompositionRoot);

        Assert.Empty(result.CompilationErrors);

        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "ERGO018");
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "ERGO024");
    }

    /// <summary>
    ///     A message no handler claims at all is ERGO005's verdict. ERGO024 refines it rather
    ///     than repeating it: it fires only where coverage exists and the filter empties it.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void AMessageWithNoCoverageAtAll_StaysERGO005()
    {
        var result = GeneratorTestHost.RunWithAllCandidates("""
            using System.Threading.Tasks;
            using Stella.Ergosfare.Commands.Abstractions;

            namespace TestApp
            {
                public sealed record OrphanCommand : ICommand;

                public class Caller(ICommandMediator mediator)
                {
                    public ValueTask Fire(OrphanCommand command)
                        => mediator.SendAsync(command, ["Admin"]);
                }
            }
            """,
            buildProperties: CompositionRoot);

        Assert.Empty(result.CompilationErrors);

        var diagnostic = Assert.Single(result.GeneratorDiagnostics);
        Assert.Equal("ERGO005", diagnostic.Id);
    }

    /// <summary>
    ///     A hand-written <c>Register</c> call says which messages a type handles, not which
    ///     groups it declares. Answering "nobody is in this set" over evidence that cannot
    ///     report a group would be a guess, so the message is left alone.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void ManuallyRegisteredCoverage_IsNotJudged()
    {
        var result = GeneratorTestHost.RunWithAllCandidates("""
            using System.Threading.Tasks;
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Attributes;

            namespace TestApp
            {
                public sealed record LoginCommand : ICommand;

                [Group("Admin")]
                public sealed class AdminLoginHandler : ICommandHandler<LoginCommand>
                {
                    public ValueTask HandleAsync(LoginCommand message, ErgosfareContext context) => default;
                }

                [ExcludeFromDiscovery]
                public sealed class ManualLoginHandler : ICommandHandler<LoginCommand>
                {
                    public ValueTask HandleAsync(LoginCommand message, ErgosfareContext context) => default;
                }

                public static class Boot
                {
                    public static void Configure(CommandModuleBuilder commands)
                        => commands.Register<ManualLoginHandler>();
                }

                public class Caller(ICommandMediator mediator)
                {
                    public ValueTask Fire(LoginCommand command) => mediator.SendAsync(command);
                }
            }
            """,
            buildProperties: CompositionRoot);

        Assert.Empty(result.CompilationErrors);
        Assert.Empty(result.GeneratorDiagnostics);
    }

    /// <summary>
    ///     A manifest records a set it could not read as no set, so an empty one read back
    ///     from a referenced assembly proves nothing and is left alone. A recorded set with
    ///     names in it was readable by construction, and is judged.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void ARecordedGroupSet_IsJudged_AndARecordedEmptyOneIsNot()
    {
        const string root = """
            using System.Threading.Tasks;
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Attributes;
            using Stella.Ergosfare.Commands.Abstractions;

            namespace TestApp
            {
                [Group("Admin")]
                public sealed class LibCommandHandler : ICommandHandler<TestLib.LibCommand>
                {
                    public ValueTask HandleAsync(TestLib.LibCommand message, ErgosfareContext context) => default;
                }
            }
            """;

        const string libraryTemplate = """
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.DispatchSites;

            [assembly: DispatchManifest(1)]
            [assembly: DispatchSite("TestLib.LibCommand", DispatchKind.Command, false, GROUPS)]

            namespace TestLib
            {
                public sealed record LibCommand : ICommand;
            }
            """;

        var judged = GeneratorTestHost.RunWithAllCandidates(root,
            libraries: [("Ergosfare.GroupedSiteLibrary", libraryTemplate.Replace("GROUPS", """Groups = new string[] { "User" }"""))],
            buildProperties: CompositionRoot);

        Assert.Empty(judged.CompilationErrors);

        var diagnostic = Assert.Single(judged.GeneratorDiagnostics);
        Assert.Equal("ERGO024", diagnostic.Id);
        Assert.Equal(Location.None, diagnostic.Location);
        Assert.Contains("Ergosfare.GroupedSiteLibrary", diagnostic.GetMessage());

        var unjudged = GeneratorTestHost.RunWithAllCandidates(root,
            libraries: [("Ergosfare.UngroupedSiteLibrary", libraryTemplate.Replace(", GROUPS", string.Empty))],
            buildProperties: CompositionRoot);

        Assert.Empty(unjudged.CompilationErrors);
        Assert.Empty(unjudged.GeneratorDiagnostics);
    }
}
