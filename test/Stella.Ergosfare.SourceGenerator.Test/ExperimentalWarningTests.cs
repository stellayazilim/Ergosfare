using Microsoft.CodeAnalysis;

namespace Stella.Ergosfare.SourceGenerator.Test;

public sealed class ExperimentalWarningTests
{
    [Theory]
    [InlineData("ERGOEXP001", "Stella.Ergosfare.Core.Abstractions.Attributes.ResultAdapterAttribute")]
    [InlineData("ERGOEXP002", "Stella.Ergosfare.Plugins.Abstractions.Hook")]
    [InlineData("ERGOEXP003", "Stella.Ergosfare.Core.Abstractions.Streaming.StreamInfo")]
    public void UnsuppressedUse_IsAWarning(string id, string type)
    {
        var result = GeneratorTestHost.Run(
            $"public static class Probe {{ public static System.Type Value => typeof({type}); }}",
            diagnosticOptions: new Dictionary<string, ReportDiagnostic> { [id] = ReportDiagnostic.Default });

        Assert.Empty(result.CompilationErrors);
        Assert.Contains(result.OutputCompilation.GetDiagnostics(),
            diagnostic => diagnostic.Id == id && diagnostic.Severity == DiagnosticSeverity.Warning);
    }
}
