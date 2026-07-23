using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Stella.Ergosfare.SourceGenerator.Test;

/// <summary>
///     Drives <see cref="ErgosfareRegistrationGenerator"/> against an in-memory compilation
///     that references the real Ergosfare assemblies (resolved from the test process's
///     trusted platform assemblies).
/// </summary>
internal static class GeneratorTestHost
{
    private static readonly Lazy<ImmutableArray<MetadataReference>> AllReferences =
        new(() => LoadReferences(includeModuleBuilders: true));

    private static readonly Lazy<ImmutableArray<MetadataReference>> ReferencesWithoutModuleBuilders =
        new(() => LoadReferences(includeModuleBuilders: false));

    public sealed record GeneratorRunResult(
        Compilation OutputCompilation,
        GeneratorDriverRunResult DriverResult,
        ImmutableArray<Diagnostic> GeneratorDiagnostics)
    {
        /// <summary>The single generated source file's text (fails the test if none/multiple).</summary>
        public string GeneratedSource => Assert.Single(DriverResult.GeneratedTrees).ToString();

        /// <summary>Compilation errors of the augmented (user + generated) compilation.</summary>
        public IEnumerable<Diagnostic> CompilationErrors =>
            OutputCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error);
    }

    /// <summary>
    ///     Runs the generator over <paramref name="source"/>. Set
    ///     <paramref name="referenceModuleBuilders"/> to <c>false</c> to simulate a project
    ///     referencing only the abstractions packages (no DI module builders).
    /// </summary>
    public static GeneratorRunResult Run(string source, bool referenceModuleBuilders = true)
    {
        var compilation = CSharpCompilation.Create(
            "Ergosfare.SourceGeneratorTestApp",
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest))],
            referenceModuleBuilders ? AllReferences.Value : ReferencesWithoutModuleBuilders.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new ErgosfareRegistrationGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        return new GeneratorRunResult(outputCompilation, driver.GetRunResult(), diagnostics);
    }

    private static ImmutableArray<MetadataReference> LoadReferences(bool includeModuleBuilders)
    {
        var trustedAssemblies = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;

        return trustedAssemblies
            .Split(Path.PathSeparator)
            .Where(path => path.Length > 0)
            .Where(path => includeModuleBuilders
                           || !Path.GetFileNameWithoutExtension(path)
                               .Contains("Extensions.MicrosoftDependencyInjection", StringComparison.Ordinal))
            .Select(MetadataReference (path) => MetadataReference.CreateFromFile(path))
            .ToImmutableArray();
    }
}
