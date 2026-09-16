using Microsoft.CodeAnalysis;
using System.Reflection;
using Stella.Ergosfare.Core.Abstractions.Planning;

namespace Stella.Ergosfare.SourceGenerator.Test;

/// <summary>
///     Discovery attributes in generated registration: <c>[DiscoveryKey]</c> gates types
///     behind key patterns, <c>[ExcludeFromDiscovery]</c> removes them from discovery
///     entirely. Semantics are asserted by compiling literal selections and reading the generated selection arrays.
/// </summary>
public class DiscoveryKeyTests
{
    [Theory]
    [InlineData("", "", true)]
    [InlineData("a", "", false)]
    [InlineData("", "a", false)]
    [InlineData("a", "a", true)]
    [InlineData("a", "A", false)]
    [InlineData("abc", "a*", true)]
    [InlineData("a", "a*", true)]
    [InlineData("b", "a*", false)]
    [InlineData("", "*", true)]
    [InlineData("anything", "*", true)]
    [InlineData("reporting.daily", "reporting.*", true)]
    [InlineData("reporting", "reporting.*", false)]
    public void GeneratedSelection_AppliesExactOrPrefixSemantics(string key, string pattern, bool expected)
    {
        var result = GeneratorTestHost.RunWithAllCandidates($$"""
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Attributes;

            namespace TestApp
            {
                [DiscoveryKey("{{key}}")]
                public sealed record SelectedCommand : ICommand;
            }
            """, referenceModuleBuilders: false);

        var selected = new DiscoveryHarness(result).Run(pattern);
        if (expected)
            Assert.Equal(["SelectedCommand"], selected);
        else
            Assert.Empty(selected);
    }

    /// <summary>
    ///     Compiles each requested selection and reads its generated type array.
    /// </summary>
    private sealed class DiscoveryHarness(GeneratorTestHost.GeneratorRunResult initial)
    {
        public IReadOnlyList<string> Run(string? pattern = null)
        {
            var literal = Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(pattern ?? "", true);
            var original = initial.OutputCompilation.SyntaxTrees.First();
            var selection = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText($$"""
                internal static class DiscoverySelection {
                    internal static void Select(Stella.Ergosfare.Core.Abstractions.Planning.DispatchPlanCatalog catalog) {
                        new Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection.CommandModuleBuilder(catalog).AddGenerated({{literal}});
                        new Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection.QueryModuleBuilder(catalog).AddGenerated({{literal}});
                        new Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection.EventModuleBuilder(catalog).AddGenerated({{literal}});
                    }
                }
                """);
            var compilation = initial.OutputCompilation.RemoveAllSyntaxTrees().AddSyntaxTrees(original, selection);
            Microsoft.CodeAnalysis.GeneratorDriver driver = Microsoft.CodeAnalysis.CSharp.CSharpGeneratorDriver.Create(
                new ErgosfareRegistrationGenerator().AsSourceGenerator());
            driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
            Assert.DoesNotContain(diagnostics, d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
            using var stream = new MemoryStream();
            var emit = output.Emit(stream);
            Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
            var assembly = Assembly.Load(stream.ToArray());
            var registrations = assembly.GetType("Stella.Ergosfare.Generated.ErgosfareGeneratedRegistrations", true)!;
            var catalog = new DispatchPlanCatalog();
            GeneratorTestHost.SelectionFor(registrations).Invoke(null, [catalog]);
            return catalog.Selections.Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        }
    }

    [Fact]
    public void KeyedTypes_AreGatedOutOfDefaultDiscovery_AndSelectedByKey()
    {
        var result = GeneratorTestHost.RunWithAllCandidates("""
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Attributes;

            namespace TestApp
            {
                public sealed record PlainCommand : ICommand;

                [DiscoveryKey("reporting")]
                public sealed record ReportCommand : ICommand;
            }
            """,
            referenceModuleBuilders: false);

        Assert.Empty(result.GeneratorDiagnostics);
        var harness = new DiscoveryHarness(result);

        Assert.Equal(["PlainCommand"], harness.Run());
        Assert.Equal(["ReportCommand"], harness.Run("reporting"));
        Assert.Equal(["PlainCommand", "ReportCommand"], harness.Run("*"));
        Assert.Empty(harness.Run("other"));
    }

    [Fact]
    public void PrefixGlob_SelectsKeysByPrefix()
    {
        var result = GeneratorTestHost.RunWithAllCandidates("""
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Attributes;

            namespace TestApp
            {
                [DiscoveryKey("reporting.daily")]
                public sealed record DailyReport : ICommand;

                [DiscoveryKey("reporting.monthly")]
                public sealed record MonthlyReport : ICommand;

                [DiscoveryKey("billing")]
                public sealed record BillingRun : ICommand;
            }
            """,
            referenceModuleBuilders: false);

        var harness = new DiscoveryHarness(result);

        Assert.Equal(["DailyReport", "MonthlyReport"], harness.Run("reporting.*"));
        Assert.Empty(harness.Run());
    }

    [Fact]
    public void EmptyStringKeyAlongsideOthers_KeepsTypeInDefaultDiscovery()
    {
        var result = GeneratorTestHost.RunWithAllCandidates("""
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Attributes;

            namespace TestApp
            {
                [DiscoveryKey("", "debug")]
                public sealed record DebugCommand : ICommand;
            }
            """,
            referenceModuleBuilders: false);

        var harness = new DiscoveryHarness(result);

        Assert.Equal(["DebugCommand"], harness.Run());
        Assert.Equal(["DebugCommand"], harness.Run("debug"));
    }

    [Fact]
    public void KeyedHandler_DescriptorsRegisterOnlyWhenSelected()
    {
        var result = GeneratorTestHost.RunWithAllCandidates("""
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Attributes;
            using System.Threading.Tasks;

            namespace TestApp
            {
                public sealed record Ping : ICommand;

                [DiscoveryKey("reporting")]
                public sealed class ReportingPingHandler : ICommandHandler<Ping>
                {
                    public ValueTask HandleAsync(Ping message, Stella.Ergosfare.Core.Abstractions.ErgosfareContext context)
                        => default;
                }
            }
            """,
            referenceModuleBuilders: false);

        var harness = new DiscoveryHarness(result);

        // The keyed handler's pre-computed descriptor stays out of default discovery and
        // arrives only when its key is selected.
        Assert.Equal(["Ping"], harness.Run());
        Assert.Equal(["Ping", "ReportingPingHandler"], harness.Run("reporting"));
    }

    [Fact]
    public void ExcludedType_ProducesNoRegistrationAndNoDiagnostics()
    {
        var result = GeneratorTestHost.RunWithAllCandidates("""
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Attributes;

            namespace TestApp
            {
                public sealed record PlainCommand : ICommand;

                [ExcludeFromDiscovery]
                public sealed record ManuallyWired : ICommand;
            }
            """,
            referenceModuleBuilders: false);

        Assert.Empty(result.GeneratorDiagnostics);

        // No registration — which is what the exclusion promises, and what this asserts.
        // It does get a dispatch root: rooting only lets a dispatch close its generic
        // without MakeGenericType and selects nothing into any container, so a hidden type
        // being dispatchable at all is reason enough. Asserting the name appeared nowhere
        // was a stronger claim than the exclusion makes.
        Assert.DoesNotContain("participants.Add(typeof(global::TestApp.ManuallyWired)", result.GeneratedSource);
        Assert.DoesNotContain("Register(typeof(global::TestApp.ManuallyWired)", result.GeneratedSource);
        Assert.DoesNotContain("AddMessage<global::TestApp.ManuallyWired>();", result.GeneratedSource);

        var harness = new DiscoveryHarness(result);
        Assert.Equal(["PlainCommand"], harness.Run("*"));
    }

    [Fact]
    public void AssemblyLevelDiscoveryKey_GatesTheLibrarysUntaggedTypes()
    {
        var result = GeneratorTestHost.RunWithAllCandidates("""
            using Stella.Ergosfare.Commands.Abstractions;

            namespace TestApp
            {
                public sealed record AppCommand : ICommand;
            }
            """,
            libraries:
            [
                ("Ergosfare.TestLibrary.AssemblyKeyed", """
                    using Stella.Ergosfare.Commands.Abstractions;
                    using Stella.Ergosfare.Core.Abstractions.Attributes;

                    [assembly: DiscoveryKey("lib")]

                    namespace TestLib
                    {
                        public sealed record LibCommand : ICommand;

                        [DiscoveryKey("special")]
                        public sealed record SpecialLibCommand : ICommand;
                    }
                    """),
            ]);

        Assert.Empty(result.GeneratorDiagnostics);
        var harness = new DiscoveryHarness(result);

        // The assembly-level key gates the library's untagged types; a type-level key
        // overrides the assembly default.
        Assert.Equal(["AppCommand"], harness.Run());
        Assert.Equal(["LibCommand"], harness.Run("lib"));
        Assert.Equal(["SpecialLibCommand"], harness.Run("special"));
    }

    [Fact]
    public void ExcludedLibraryAssembly_IsNotScannedAtAll()
    {
        var result = GeneratorTestHost.RunWithAllCandidates("""
            using Stella.Ergosfare.Commands.Abstractions;

            namespace TestApp
            {
                public sealed record AppCommand : ICommand;
            }
            """,
            libraries:
            [
                ("Ergosfare.TestLibrary.Excluded", """
                    using Stella.Ergosfare.Commands.Abstractions;
                    using Stella.Ergosfare.Core.Abstractions.Attributes;

                    [assembly: ExcludeFromDiscovery]

                    namespace TestLib
                    {
                        public sealed record LibCommand : ICommand;

                        internal sealed record HiddenLibCommand : ICommand;
                    }
                    """),
            ]);

        // No registrations from the excluded assembly — and no ERGO002 for its
        // internals either: the exclusion is deliberate.
        Assert.Empty(result.GeneratorDiagnostics);
        Assert.DoesNotContain("LibCommand", result.GeneratedSource);

        var harness = new DiscoveryHarness(result);
        Assert.Equal(["AppCommand"], harness.Run("*"));
    }
}
