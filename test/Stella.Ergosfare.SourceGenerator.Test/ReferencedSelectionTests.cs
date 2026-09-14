using Microsoft.CodeAnalysis;

namespace Stella.Ergosfare.SourceGenerator.Test;

public class ReferencedSelectionTests
{
    private const string Library = """
        using System.Threading.Tasks;
        using Stella.Ergosfare.Core.Abstractions;
        using Stella.Ergosfare.Commands.Abstractions;
        using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
        namespace Application {
            public class Ping : ICommand { }
            public sealed class Handler : ICommandHandler<Ping> {
                public ValueTask HandleAsync(Ping message, ErgosfareContext context) => default;
            }
            public static class Configuration {
                public static void AddApplication(CommandModuleBuilder builder) => Configure(builder);
                private static void Configure(CommandModuleBuilder builder) => builder.AddGenerated();
            }
        }
        """;

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public void ReferencedConfiguration_SelectsOnlyWhenCalled(bool callConfiguration, bool sourceReference)
    {
        var result = GeneratorTestHost.Run($$"""
            public static class Host {
                public static void Configure(Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection.CommandModuleBuilder builder) {
                    {{(callConfiguration ? "Application.Configuration.AddApplication(builder);" : "")}}
                }
                public static System.Threading.Tasks.ValueTask Dispatch(Stella.Ergosfare.Commands.Abstractions.ICommandMediator mediator)
                    => mediator.SendAsync(new Application.Ping());
            }
            """, libraries: [("ApplicationSelectionLibrary", Library)], generateLibrarySelections: true, sourceLibraryReferences: sourceReference,
            buildProperties: new Dictionary<string, string> { ["ErgosfareCompositionRoot"] = "true" });

        Assert.Empty(result.CompilationErrors);
        if (callConfiguration)
        {
            Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Severity == DiagnosticSeverity.Error);
            Assert.Contains("global::Application.Handler", result.GeneratedSource);
            Assert.Contains("AddGeneratedSelection(1", result.GeneratedSource);
        }
        else
        {
            Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "ERGO005");
            Assert.DoesNotContain("global::Application.Handler", result.GeneratedSource);
        }
    }

    [Fact]
    public void RegistrationOnlyAssembly_ExportsGroupedDispatchesForRootPlans()
    {
        var library = Library.Replace("public sealed class Handler", "[Stella.Ergosfare.Core.Abstractions.Attributes.Group(\"audit\")] public sealed class Handler")
            + """
            public static class LibraryDispatch {
                public static System.Threading.Tasks.ValueTask Send(Stella.Ergosfare.Commands.Abstractions.ICommandMediator mediator)
                    => mediator.SendAsync(new Application.Ping(), ["audit"], System.Threading.CancellationToken.None);
            }
            """;
        var result = GeneratorTestHost.Run("""
            public static class Host {
                public static void Configure(Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection.CommandModuleBuilder builder)
                    => Application.Configuration.AddApplication(builder);
            }
            """, libraries: [("GroupedApplicationSelectionLibrary", library)], generateLibrarySelections: true,
            buildProperties: new Dictionary<string, string> { ["ErgosfareCompositionRoot"] = "true" });
        Assert.Empty(result.CompilationErrors);
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("\"audit\"", result.GeneratedSource);
        Assert.Contains("new Staged", result.GeneratedSource);
    }

    [Fact]
    public void RegistrationOnlyAssembly_CompilesPublicCallsWithoutExecutablePlans()
    {
        var result = GeneratorTestHost.Run(Library,
            buildProperties: new Dictionary<string, string> { ["ErgosfareGeneratePlans"] = "false" });
        Assert.Empty(result.CompilationErrors);
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Severity == DiagnosticSeverity.Error);
        var manifest = Assert.Single(result.DriverResult.GeneratedTrees.Where(t => t.FilePath.EndsWith("ErgosfareSelectionManifest.g.cs"))).ToString();
        Assert.DoesNotContain(result.DriverResult.GeneratedTrees, t => t.FilePath.EndsWith("ErgosfareRegistrations.g.cs"));
        Assert.Contains("GeneratedSelectionAttribute", manifest);
        Assert.DoesNotContain("new Staged", manifest);
        Assert.DoesNotContain("PopulateGeneratedSelections", manifest);
    }
}
