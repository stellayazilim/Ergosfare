using Microsoft.CodeAnalysis;
using Stella.Ergosfare.SourceGenerator.Symbols;

namespace Stella.Ergosfare.SourceGenerator.Test;

[Trait("Category", "Unit")]
public class ConstructionAnalysisCoverageTests
{
    [Theory]
    [InlineData("'x'", "'x'")]
    [InlineData("true", "true")]
    [InlineData("false", "false")]
    [InlineData("12", "12")]
    [InlineData("12L", "12L")]
    [InlineData("(sbyte)-2", "(sbyte)-2")]
    [InlineData("(byte)2", "(byte)2")]
    [InlineData("(short)-3", "(short)-3")]
    [InlineData("(ushort)3", "(ushort)3")]
    [InlineData("4U", "4U")]
    [InlineData("5UL", "5UL")]
    [InlineData("Key.One", "(global::Key)(1)")]
    [InlineData("typeof(string)", "typeof(string)")]
    [InlineData("typeof(System.Collections.Generic.List<>)", null)]
    [InlineData("1.25", null)]
    [InlineData("null", null)]
    [InlineData("new int[] { 1 }", null)]
    public void KeyLiterals_PreserveBoxedIdentityOrDeclineUnsupportedKeys(string key, string? expected)
    {
        var result = GeneratorTestHost.Run($$"""
            using Microsoft.Extensions.DependencyInjection;
            public enum Key { One = 1 }
            public sealed class Consumer
            {
                public Consumer([FromKeyedServices({{key}})] object dependency) { }
            }
            """);
        Assert.Empty(result.CompilationErrors);
        var symbol = result.OutputCompilation.GetTypeByMetadataName("Consumer")!;
        var attribute = Assert.Single(symbol.InstanceConstructors[0].Parameters[0].GetAttributes());
        Assert.Equal(expected, ConstructionAnalyzer.GetServiceKeyLiteral(attribute, symbol.ContainingAssembly));
    }

    [Theory]
    [InlineData("private Consumer() {}")]
    [InlineData("public Consumer([ServiceKey] object key) {}")]
    [InlineData("public Consumer(int? value) {}")]
    [InlineData("public Consumer(System.Span<int> value) {}")]
    [InlineData("public Consumer([FromKeyedServices(1.25)] object value) {}")]
    public void UnsafeDirectConstructionShapes_StayWithTheContainer(string constructor)
    {
        var result = GeneratorTestHost.Run("using Microsoft.Extensions.DependencyInjection; public sealed class Consumer { " + constructor + " }");
        Assert.Empty(result.CompilationErrors);
        var symbol = result.OutputCompilation.GetTypeByMetadataName("Consumer")!;
        Assert.False(ConstructionAnalyzer.IsDirectlyConstructible(symbol));
        Assert.Null(ConstructionAnalyzer.TryBuildConstructionExpression(symbol, "global::Consumer", symbol.ContainingAssembly,
            "provider", true, out _));
        Assert.True(ConstructionAnalyzer.CanRegisterParticipant(symbol));
    }
}
