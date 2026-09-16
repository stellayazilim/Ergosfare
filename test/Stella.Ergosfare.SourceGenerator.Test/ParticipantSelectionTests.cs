using Microsoft.CodeAnalysis;

namespace Stella.Ergosfare.SourceGenerator.Test;

public class ParticipantSelectionTests
{
    private const string Candidates = """
        using System.Threading.Tasks;
        using Stella.Ergosfare.Core.Abstractions;
        using Stella.Ergosfare.Core.Abstractions.Attributes;
        using Stella.Ergosfare.Commands.Abstractions;
        namespace SelectionProbe {
            public class Ping : ICommand { }
            public class Admin : ICommand { }
            [DiscoveryKey("users")]
            public sealed class UserHandler : ICommandHandler<Ping> {
                public ValueTask HandleAsync(Ping message, ErgosfareContext context) => default;
            }
            [ExcludeFromDiscovery, Group("admin")]
            public sealed class AdminHandler : ICommandHandler<Admin> {
                public ValueTask HandleAsync(Admin message, ErgosfareContext context) => default;
            }
        }
        """;

    private static GeneratorTestHost.GeneratorRunResult Run(string selection, string dispatch = "", bool referenced = false)
        => GeneratorTestHost.Run((referenced ? "" : Candidates) + $$"""

            namespace SelectionProbe {
                public static class App {
                    public static void Configure(Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection.CommandModuleBuilder builder) {
                        {{selection}}
                    }
                    {{dispatch}}
                }
            }
            """, libraries: referenced ? [("SelectionLibrary", Candidates)] : null,
            buildProperties: new Dictionary<string, string> { ["ErgosfareCompositionRoot"] = "true" });

    [Fact]
    public void NoSelection_DoesNotEmitCandidatePlans()
    {
        var result = Run("");
        Assert.Empty(result.CompilationErrors);
        Assert.DoesNotContain("global::SelectionProbe.UserHandler", result.GeneratedSource);
        Assert.DoesNotContain("global::SelectionProbe.AdminHandler", result.GeneratedSource);
        Assert.DoesNotContain("new Staged", result.GeneratedSource);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BulkSelection_OnlyPlansSelectedKey(bool referenced)
    {
        var result = Run("builder.AddGenerated(\"users\");", referenced: referenced);
        Assert.Empty(result.CompilationErrors);
        Assert.Contains("global::SelectionProbe.UserHandler", result.GeneratedSource);
        Assert.DoesNotContain("global::SelectionProbe.AdminHandler", result.GeneratedSource);
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Severity == DiagnosticSeverity.Error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExplicitSelection_IncludesExcludedHandlerWithGroupValidation(bool referenced)
    {
        var result = Run("builder.Register<AdminHandler>();", """
            public static System.Threading.Tasks.ValueTask Dispatch(Stella.Ergosfare.Commands.Abstractions.ICommandMediator mediator, Admin message)
                => mediator.SendAsync(message, "wrong", System.Threading.CancellationToken.None);
            """, referenced);
        Assert.Empty(result.CompilationErrors);
        Assert.Contains("global::SelectionProbe.AdminHandler", result.GeneratedSource);
        Assert.DoesNotContain("global::SelectionProbe.UserHandler", result.GeneratedSource);
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "ERGO024");
        Assert.DoesNotContain("ManualRegistrationAttribute", result.GeneratedSource);
    }

    [Fact]
    public void UnselectedHandler_DoesNotCoverDispatch()
    {
        var result = Run("", """
            public static System.Threading.Tasks.ValueTask Dispatch(Stella.Ergosfare.Commands.Abstractions.ICommandMediator mediator, Ping message)
                => mediator.SendAsync(message);
            """);
        Assert.Empty(result.CompilationErrors);
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "ERGO005");
    }

    [Fact]
    public void DynamicDiscoveryPattern_IsACompileError()
    {
        var result = Run("builder.AddGenerated(System.Environment.GetEnvironmentVariable(\"SELECTION\"));");
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "ERGO018");
    }

    [Fact]
    public void ChainedGeneratedAndExplicitSelections_AreAllCollected()
    {
        var result = GeneratorTestHost.Run("using Stella.Ergosfare.Generated;\n" + Candidates + """
            namespace SelectionProbe {
                public static class App {
                    public static void Configure(Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection.CommandModuleBuilder builder)
                        => builder.AddGenerated().AddGenerated("users").Register<AdminHandler>();
                }
            }
            """);
        Assert.Empty(result.CompilationErrors);
        Assert.Contains("global::SelectionProbe.UserHandler", result.GeneratedSource);
        Assert.Contains("global::SelectionProbe.AdminHandler", result.GeneratedSource);
    }

    [Fact]
    public void ExplicitReferencedSelection_WorksWithAutomaticScanningDisabled()
    {
        var result = GeneratorTestHost.Run("""
            public static class App {
                public static void Configure(Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection.CommandModuleBuilder builder)
                    => builder.Register<SelectionProbe.AdminHandler>();
                public static System.Threading.Tasks.ValueTask Dispatch(Stella.Ergosfare.Commands.Abstractions.ICommandMediator mediator, SelectionProbe.Admin message)
                    => mediator.SendAsync(message, "admin", System.Threading.CancellationToken.None);
            }
            """, libraries: [("SelectionWithoutScan", Candidates)], scanReferences: false);
        Assert.Empty(result.CompilationErrors);
        Assert.Contains("global::SelectionProbe.AdminHandler", result.GeneratedSource);
        Assert.Contains("AddStagedPlan<global::SelectionProbe.Admin>", result.GeneratedSource);
        Assert.DoesNotContain("global::SelectionProbe.UserHandler", result.GeneratedSource);
    }

    [Fact]
    public void SelectedBaseHandler_CoversDerivedMessage_WithoutSelectingItsDirectHandler()
    {
        var result = GeneratorTestHost.Run(Candidates + """
            namespace SelectionProbe {
                public sealed class DerivedPing : Ping { }
                public sealed class UnselectedDirect : Stella.Ergosfare.Commands.Abstractions.ICommandHandler<DerivedPing> {
                    public System.Threading.Tasks.ValueTask HandleAsync(DerivedPing message, Stella.Ergosfare.Core.Abstractions.ErgosfareContext context) => default;
                }
                public static class App {
                    public static void Configure(Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection.CommandModuleBuilder builder)
                        => builder.Register<UserHandler>();
                    public static System.Threading.Tasks.ValueTask Dispatch(Stella.Ergosfare.Commands.Abstractions.ICommandMediator mediator, DerivedPing message)
                        => mediator.SendAsync(message);
                }
            }
            """, buildProperties: new Dictionary<string, string> { ["ErgosfareCompositionRoot"] = "true" });
        Assert.Empty(result.CompilationErrors);
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("global::SelectionProbe.UserHandler", result.GeneratedSource);
        Assert.DoesNotContain("global::SelectionProbe.UnselectedDirect", result.GeneratedSource);
    }
}
