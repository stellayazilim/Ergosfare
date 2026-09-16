using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Stella.Ergosfare.Plugins.Outbox.Generators;

namespace OutboxGeneratorTests;

[Trait("Category", "Unit")]
public sealed class GeneratorTests
{
    private const string Preamble = """
        using System;
        using Stella.Ergosfare.Plugins.Outbox;
        namespace Stella.Ergosfare.Plugins.Outbox {
            [AttributeUsage(AttributeTargets.Class)]
            public sealed class OutboxMessageAttribute(string contract = null) : Attribute { }
            [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
            public sealed class OutboxCodecManifestAttribute(Type codec) : Attribute { }
            public static class GeneratedOutboxMessages {
                public static void Register<T>(string name, Func<T, byte[]> write, Func<byte[], T> read) { }
            }
        }
        """;

    private static (GeneratorDriverRunResult Run, Compilation Compilation) Generate(string source)
    {
        var parse = new CSharpParseOptions(LanguageVersion.Preview);
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("OutboxGeneratorProbe",
            [CSharpSyntaxTree.ParseText(Preamble + source, parse)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new OutboxMessageGenerator().AsSourceGenerator()], parseOptions: parse);
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var updated, out _);
        return (driver.GetRunResult(), updated);
    }

    [Theory]
    [InlineData("[OutboxMessage] public sealed class Message { public int Id { get; set; } }")]
    [InlineData("[OutboxMessage] public sealed partial class Message { public object Data { get; set; } }")]
    [InlineData("[OutboxMessage] public sealed partial class Message<T> { public T Data { get; set; } }")]
    [InlineData("[OutboxMessage] public sealed partial class Message { public int Id; }")]
    [InlineData("[OutboxMessage] public sealed partial class Message { public int Id { get; } }")]
    [InlineData("[OutboxMessage] public sealed partial class Message { [System.Text.Json.Serialization.JsonIgnore] public int Id { get; set; } }")]
    public void UnsupportedShapes_FailExplicitly(string source)
    {
        var result = Generate(source);
        Assert.Contains(result.Run.Diagnostics, d => d.Id == "OUTBOX001" && d.Severity == DiagnosticSeverity.Error);
        Assert.Empty(result.Run.GeneratedTrees);
    }

    [Fact]
    public void DuplicateContracts_AreRejected()
    {
        var result = Generate("""
            [OutboxMessage("same")] public sealed partial record First(int Id);
            [OutboxMessage("same")] public sealed partial record Second(int Id);
            """);
        Assert.Contains(result.Run.Diagnostics, d => d.Id == "OUTBOX002");
    }

    [Fact]
    public void RecordAndPoco_CodecsCompile_WithoutJsonGeneratorOrReflection()
    {
        var result = Generate("""
            [OutboxMessage] public sealed partial record Message(Guid Id, decimal Price, string Name, int? Count);
            [OutboxMessage] public sealed partial class Other {
                public required string Name { get; init; }
                public DateTime When { get; set; }
                public System.Collections.Generic.List<int?> Values { get; set; }
            }
            """);
        Assert.DoesNotContain(result.Run.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Empty(result.Compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Equal(2, result.Run.GeneratedTrees.Length);
        var generated = string.Join("\n", result.Run.GeneratedTrees.Select(t => t.ToString()));
        Assert.DoesNotContain("JsonSerializerContext", generated);
        Assert.DoesNotContain("GetType(", generated);
        Assert.Contains("Utf8JsonWriter", generated);
    }

    [Fact]
    public void ReferencedMessageAssembly_ImportsItsCodecWithoutRuntimeScanning()
    {
        var library = Generate("[OutboxMessage] public sealed partial record ExternalMessage(int Id);");
        using var image = new MemoryStream();
        var emit = library.Compilation.Emit(image);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics));
        var parse = new CSharpParseOptions(LanguageVersion.Preview);
        var consumer = CSharpCompilation.Create("Consumer",
            [CSharpSyntaxTree.ParseText("public class Host { }", parse)],
            library.Compilation.References.Append(MetadataReference.CreateFromImage(image.ToArray())),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new OutboxMessageGenerator().AsSourceGenerator()], parseOptions: parse);
        driver = driver.RunGeneratorsAndUpdateCompilation(consumer, out var updated, out _);
        Assert.Empty(updated.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
        var generated = Assert.Single(driver.GetRunResult().GeneratedTrees).ToString();
        Assert.Contains("OutboxCodec_ExternalMessage_0.Register();", generated);
    }
}
