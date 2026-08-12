using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

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

    private static readonly ConcurrentDictionary<string, byte[]> LibraryImages = new(StringComparer.Ordinal);

    private static readonly ConcurrentDictionary<string, Assembly> LoadedLibraries = new(StringComparer.Ordinal);

    static GeneratorTestHost()
    {
        // Tests that execute emitted output need the in-memory test libraries resolvable
        // by name — the emitted app assembly references them like any project reference.
        // Because loaded assemblies are cached by identity, a test that executes emitted
        // code must give its library a name unique to that library's source.
        AppDomain.CurrentDomain.AssemblyResolve += static (_, args) =>
        {
            var name = new AssemblyName(args.Name).Name;

            return name is not null && LibraryImages.TryGetValue(name, out var image)
                ? LoadedLibraries.GetOrAdd(name, static (_, bytes) => Assembly.Load(bytes), image)
                : null;
        };
    }

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
    ///     referencing only the abstractions packages (no DI module builders). Each entry
    ///     in <paramref name="libraries"/> is compiled to an in-memory metadata reference
    ///     the app compilation references — the reference-scanning input. A non-null
    ///     <paramref name="scanReferences"/> supplies the
    ///     <c>ErgosfareSourceGeneratorScanReferences</c> build property.
    /// </summary>
    public static GeneratorRunResult Run(
        string source,
        bool referenceModuleBuilders = true,
        IReadOnlyList<(string AssemblyName, string Source)>? libraries = null,
        bool? scanReferences = null,
        IReadOnlyDictionary<string, string>? buildProperties = null,
        OutputKind outputKind = OutputKind.DynamicallyLinkedLibrary,
        IReadOnlyDictionary<string, ReportDiagnostic>? diagnosticOptions = null)
    {
        var references = referenceModuleBuilders ? AllReferences.Value : ReferencesWithoutModuleBuilders.Value;

        if (libraries is { Count: > 0 })
        {
            references = references.AddRange(libraries.Select(library =>
                // ReSharper disable once AccessToModifiedClosure
                CompileLibrary(library.AssemblyName, library.Source, references)));
        }

        // Test sources are deliberate consumers of the experimental result-adapter and
        // plugin surfaces; the opt-in suppression every real consumer would carry is baked in.
        var effectiveDiagnosticOptions = ImmutableDictionary<string, ReportDiagnostic>.Empty
            .Add("ERGOEXP001", ReportDiagnostic.Suppress)
            .Add("ERGOEXP002", ReportDiagnostic.Suppress);

        if (diagnosticOptions is not null)
        {
            // The way an .editorconfig severity override (dotnet_diagnostic.<id>.severity)
            // reaches the generator driver's diagnostic filter.
            effectiveDiagnosticOptions = effectiveDiagnosticOptions.SetItems(
                diagnosticOptions.Select(pair => new KeyValuePair<string, ReportDiagnostic>(pair.Key, pair.Value)));
        }

        var compilationOptions = new CSharpCompilationOptions(outputKind)
            .WithSpecificDiagnosticOptions(effectiveDiagnosticOptions);

        var compilation = CSharpCompilation.Create(
            "Ergosfare.SourceGeneratorTestApp",
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest))],
            references,
            compilationOptions);

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [new ErgosfareRegistrationGenerator().AsSourceGenerator()],
            optionsProvider: scanReferences is null && buildProperties is null
                ? null
                : new TestAnalyzerConfigOptionsProvider(scanReferences, buildProperties));
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        return new GeneratorRunResult(outputCompilation, driver.GetRunResult(), diagnostics);
    }

    /// <summary>Compiles a library to an in-memory metadata reference.</summary>
    private static MetadataReference CompileLibrary(
        string assemblyName,
        string source,
        ImmutableArray<MetadataReference> references)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest))],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithSpecificDiagnosticOptions(ImmutableDictionary<string, ReportDiagnostic>.Empty
                    .Add("ERGOEXP001", ReportDiagnostic.Suppress)
                    .Add("ERGOEXP002", ReportDiagnostic.Suppress)));

        using var stream = new MemoryStream();
        var emitResult = compilation.Emit(stream);

        if (!emitResult.Success)
        {
            throw new InvalidOperationException(
                $"Test library '{assemblyName}' failed to compile: " +
                string.Join(Environment.NewLine, emitResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        }

        var image = stream.ToArray();
        LibraryImages[assemblyName] = image;

        return MetadataReference.CreateFromImage(image);
    }

    /// <summary>
    ///     Analyzer-config surface exposing the generator's build properties, the way
    ///     MSBuild's <c>CompilerVisibleProperty</c> plumbing would. Property names in
    ///     <paramref name="buildProperties"/> are the bare MSBuild names
    ///     (<c>ErgosfareCompositionRoot</c>, …); the provider adds the
    ///     <c>build_property.</c> prefix itself.
    /// </summary>
    private sealed class TestAnalyzerConfigOptionsProvider(
        bool? scanReferences,
        IReadOnlyDictionary<string, string>? buildProperties) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new TestOptions(scanReferences, buildProperties);

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => GlobalOptions;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => GlobalOptions;

        private sealed class TestOptions : AnalyzerConfigOptions
        {
            private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

            public TestOptions(bool? scanReferences, IReadOnlyDictionary<string, string>? buildProperties)
            {
                if (scanReferences is { } scan)
                {
                    _values["build_property.ErgosfareSourceGeneratorScanReferences"] = scan ? "true" : "false";
                }

                if (buildProperties is null)
                {
                    return;
                }

                foreach (var pair in buildProperties)
                {
                    _values["build_property." + pair.Key] = pair.Value;
                }
            }

            public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value)
                => _values.TryGetValue(key, out value);
        }
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
