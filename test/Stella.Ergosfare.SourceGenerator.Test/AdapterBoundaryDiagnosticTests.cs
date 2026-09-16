namespace Stella.Ergosfare.SourceGenerator.Test;

[Trait("Category", "Unit")]
public class AdapterBoundaryDiagnosticTests
{
    [Theory]
    [InlineData("null")]
    [InlineData("typeof(MissingAdapter)")]
    public void MissingAnnotationTarget_ReportsAdapterDiagnosticWithoutCrashing(string target)
    {
        var result = GeneratorTestHost.RunWithAllCandidates($$"""
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Attributes;
            [ResultAdapter({{target}})]
            public sealed record Work : ICommand<string>;
            """);
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "ERGO011");
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id == "CS8785");
    }

    [Theory]
    [InlineData("public Adapter(int dependency) {}", true)]
    [InlineData("private Adapter() {}", true)]
    [InlineData("public Adapter() {}", false)]
    public void DefaultAdapter_MustBeDirectlyInstantiable(string constructor, bool rejected)
    {
        var result = GeneratorTestHost.Run($$"""
            using System;
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
            internal sealed class Adapter : IResultAdapter<string>
            {
                {{constructor}}
                public bool TryGetException(in string result, out Exception exception)
                { exception = null; return false; }
            }
            public static class Configure
            {
                public static void Apply(IModuleRegistry modules) => modules.UseDefaultResultAdapter(typeof(Adapter));
            }
            """);
        Assert.DoesNotContain(result.CompilationErrors, d => d.Id.StartsWith("CS"));
        Assert.Equal(rejected, result.GeneratorDiagnostics.Any(d => d.Id == "ERGO021"));
    }

    [Fact]
    public void DefaultAdapterInsideOpenContainer_IsDiagnosedAsOpaque()
    {
        var result = GeneratorTestHost.Run("""
            using System;
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
            public class Outer<T>
            {
                public sealed class Adapter : IResultAdapter<string>
                { public bool TryGetException(in string result, out Exception exception) { exception = null; return false; } }
            }
            public static class Configure
            {
                public static void Apply(IModuleRegistry modules) => modules.UseDefaultResultAdapter(typeof(Outer<>.Adapter));
            }
            """);
        Assert.DoesNotContain(result.CompilationErrors, d => d.Id.StartsWith("CS"));
        Assert.Contains(result.GeneratorDiagnostics, d => d.Id == "ERGO019");
    }
}
