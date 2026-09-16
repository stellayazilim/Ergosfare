using Stella.Ergosfare.SourceGenerator.ResultAdapters;
using Stella.Ergosfare.SourceGenerator.Symbols;

namespace Stella.Ergosfare.SourceGenerator.Test;

[Trait("Category", "Unit")]
public class TypePatternBoundaryTests
{
    [Theory]
    [InlineData("Pair<T, T>", "Pair<int, int>", true)]
    [InlineData("Pair<T, T>", "Pair<int, string>", false)]
    [InlineData("Pair<T, T>", "Single<int>", false)]
    [InlineData("Pair<T, T>", "Pair<int>", false)]
    [InlineData("Pair<T, T>", "int", false)]
    [InlineData("Pair<T, T>", "Pair<List<int>, List<int>>", true)]
    [InlineData("Outer<T>.Inner<int>", "Outer<T>.Inner<int>", true)]
    public void RepeatedAdapterParameters_MustUnifyConsistently(string pattern, string concrete, bool matches)
    {
        var matcher = new TypePatternMatcher(["T"]);
        var bindings = new string?[1];
        Assert.Equal(matches, matcher.TryMatch(pattern, concrete, bindings));
        if (matches) Assert.Equal(concrete, matcher.Render(pattern, bindings));
    }

    [Fact]
    public void PartialBindings_PreserveUnboundParametersAndFixedArguments()
    {
        var matcher = new TypePatternMatcher(["T", "U"]);
        Assert.Equal("Pair<int, List<U>>", matcher.Render("Pair<T, List<U>>", ["int", null]));
        Assert.Equal("int[]", matcher.Render("int[]", [null, null]));
        Assert.False(matcher.TryMatch("int[]", "string[]", [null, null]));
    }

    [Fact]
    public void SynchronousHandlerContract_RecordsItsResultAndCallingConvention()
    {
        var result = GeneratorTestHost.Run("""
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Handlers;
            public sealed record Work : ICommand<int>;
            public sealed class Handler : ICommand, IHandler<Work, int>
            { public int Handle(Work message, ErgosfareContext context) => 42; }
            """);
        Assert.Empty(result.CompilationErrors);
        var descriptor = Assert.Single(ContractReader.BuildDescriptors(result.OutputCompilation.GetTypeByMetadataName("Handler")!));
        Assert.False(descriptor.IsAsync);
        Assert.Equal("int", descriptor.ResultTypeExpression);
        Assert.Equal("global::Work", descriptor.MessageTypeExpression);
    }
}
