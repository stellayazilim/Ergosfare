namespace Stella.Ergosfare.SourceGenerator.Test;

/// <summary>
///     Reference scanning: the consuming compilation's generator discovers marker types in
///     referenced assemblies and registers them alongside the compilation's own — the
///     compile-time replacement for cross-assembly <c>RegisterFromAssembly</c> calls.
/// </summary>
public class ReferenceScanningTests
{
    private const string LibrarySource = """
        using Stella.Ergosfare.Commands.Abstractions;
        using System.Threading.Tasks;

        namespace TestLib
        {
            public sealed record LibPing : ICommand;

            public sealed class LibPingHandler : ICommandHandler<LibPing>
            {
                public ValueTask HandleAsync(LibPing message, Stella.Ergosfare.Core.Abstractions.IExecutionContext context)
                    => default;
            }
        }
        """;

    private const string AppSource = """
        using Stella.Ergosfare.Commands.Abstractions;

        namespace TestApp
        {
            public sealed record AppPing : ICommand;
        }
        """;

    [Fact]
    public void PublicLibraryTypes_RegisterThroughTheConsumingCompilation()
    {
        var result = GeneratorTestHost.Run(
            AppSource,
            libraries: [("Ergosfare.TestLibrary", LibrarySource)]);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);

        var source = result.GeneratedSource;

        // The library's plain message goes through the runtime fallback, its handler
        // through a pre-computed descriptor — exactly like source-declared types.
        Assert.Contains("registry.Register(typeof(global::TestLib.LibPing));", source);
        Assert.Contains(
            ".Handler(typeof(global::TestLib.LibPing), typeof(global::System.Threading.Tasks.ValueTask), typeof(global::TestLib.LibPingHandler))",
            source);

        // The compilation's own types are unaffected.
        Assert.Contains("registry.Register(typeof(global::TestApp.AppPing));", source);
    }

    [Fact]
    public void LibraryAttributes_FlowIntoDescriptorsFromMetadata()
    {
        var result = GeneratorTestHost.Run(
            AppSource,
            libraries:
            [
                ("Ergosfare.TestLibrary", """
                    using Stella.Ergosfare.Commands.Abstractions;
                    using Stella.Ergosfare.Core.Abstractions.Attributes;
                    using System.Threading.Tasks;

                    namespace TestLib
                    {
                        public sealed record LibPing : ICommand;

                        [Weight(7)]
                        [Group("lib")]
                        public sealed class LibAudit : ICommandPreInterceptor<LibPing>
                        {
                            public ValueTask<object> HandleAsync(LibPing message, Stella.Ergosfare.Core.Abstractions.IExecutionContext context)
                                => new(message);
                        }
                    }
                    """),
            ]);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);
        Assert.Contains(
            ".PreInterceptor(typeof(global::TestLib.LibPing), typeof(global::TestLib.LibAudit), 7u, new string[] { \"lib\" })",
            result.GeneratedSource);
    }

    [Fact]
    public void InternalLibraryType_WithoutIvt_IsSkippedWithErgosg002()
    {
        var result = GeneratorTestHost.Run(
            AppSource,
            libraries:
            [
                ("Ergosfare.TestLibrary", """
                    using Stella.Ergosfare.Commands.Abstractions;

                    namespace TestLib
                    {
                        internal sealed record HiddenCommand : ICommand;
                    }
                    """),
            ]);

        Assert.Empty(result.CompilationErrors);
        Assert.DoesNotContain("HiddenCommand", result.GeneratedSource);

        var diagnostic = Assert.Single(result.GeneratorDiagnostics);
        Assert.Equal("ERGOSG002", diagnostic.Id);
        Assert.Contains("HiddenCommand", diagnostic.GetMessage());
        Assert.Contains("Ergosfare.TestLibrary", diagnostic.GetMessage());
    }

    [Fact]
    public void InternalLibraryType_WithIvt_IsRegistered()
    {
        var result = GeneratorTestHost.Run(
            AppSource,
            libraries:
            [
                ("Ergosfare.TestLibrary", """
                    using Stella.Ergosfare.Commands.Abstractions;

                    [assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Ergosfare.SourceGeneratorTestApp")]

                    namespace TestLib
                    {
                        internal sealed record SharedCommand : ICommand;
                    }
                    """),
            ]);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);
        Assert.Contains("registry.Register(typeof(global::TestLib.SharedCommand));", result.GeneratedSource);
    }

    [Fact]
    public void ScanReferencesFalse_RestrictsRegistrationToTheCompilation()
    {
        var result = GeneratorTestHost.Run(
            AppSource,
            libraries: [("Ergosfare.TestLibrary", LibrarySource)],
            scanReferences: false);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);
        Assert.DoesNotContain("TestLib", result.GeneratedSource);
        Assert.Contains("registry.Register(typeof(global::TestApp.AppPing));", result.GeneratedSource);
    }

    [Fact]
    public void ScanReferencesTrue_MatchesTheDefault()
    {
        var result = GeneratorTestHost.Run(
            AppSource,
            libraries: [("Ergosfare.TestLibrary", LibrarySource)],
            scanReferences: true);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);
        Assert.Contains("registry.Register(typeof(global::TestLib.LibPing));", result.GeneratedSource);
    }

    [Fact]
    public void ErgosfareNamedAssemblies_AreNotScanned()
    {
        // Ergosfare's own assemblies are excluded by name: their handler contract
        // interfaces inherit the module markers and must never register as user types.
        var result = GeneratorTestHost.Run(
            AppSource,
            libraries: [("Stella.Ergosfare.FakeSatellite", LibrarySource)]);

        Assert.Empty(result.GeneratorDiagnostics);
        Assert.Empty(result.CompilationErrors);
        Assert.DoesNotContain("TestLib", result.GeneratedSource);
    }

    [Fact]
    public void LibraryTypes_AppearInTheirModulesBuilderExtension()
    {
        var result = GeneratorTestHost.Run(
            AppSource,
            libraries: [("Ergosfare.TestLibrary", LibrarySource)]);

        var source = result.GeneratedSource;

        // Once in RegisterAll, once in the command builder extension — the library's
        // types are indistinguishable from the compilation's own in the emitted surface.
        Assert.Contains("builder.Register(typeof(global::TestLib.LibPing));", source);
        Assert.DoesNotContain("Register(typeof(global::TestLib.LibPingHandler))", source);
    }
}
