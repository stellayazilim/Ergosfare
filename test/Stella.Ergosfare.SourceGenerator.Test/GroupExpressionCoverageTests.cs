namespace Stella.Ergosfare.SourceGenerator.Test;

[Trait("Category", "Unit")]
public class GroupExpressionCoverageTests
{
    [Theory]
    [InlineData("[\"audit\"]", true)]
    [InlineData("GroupSet.Of(\"audit\")", true)]
    [InlineData("GroupSet.Of(name)", false)]
    [InlineData("[name]", false)]
    [InlineData("[..new[] { name }]", false)]
    [InlineData("Build()", false)]
    [InlineData("Mutable", false)]
    [InlineData("Uninitialized", false)]
    [InlineData("Alias", false)]
    public void GroupExpressions_OnlySpecializeProvenConstants(string expression, bool proven)
    {
        var result = GeneratorTestHost.RunWithAllCandidates(Source(expression));
        Assert.Empty(result.CompilationErrors);
        var specialized = "new StagedPlan1(), new string[] { \"audit\" }";
        if (proven) Assert.Contains(specialized, result.GeneratedSource);
        else Assert.DoesNotContain(specialized, result.GeneratedSource);
        if (!proven) Assert.Contains("AddFilteredBroadcastPlan", result.GeneratedSource);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("default")]
    [InlineData("GroupSet.Empty")]
    public void EmptyGroupExpressions_UseDefaultPlan(string expression)
    {
        var result = GeneratorTestHost.RunWithAllCandidates(Source(expression));
        Assert.Empty(result.CompilationErrors);
        Assert.Contains("AddBroadcastPlan<global::GroupCoverage.Notice>(new StagedPlan0());", result.GeneratedSource);
        Assert.DoesNotContain("new StagedPlan1(), new string[]", result.GeneratedSource);
    }

    private static string Source(string expression) => $$"""
        using System.Threading.Tasks;
        using Stella.Ergosfare.Core.Abstractions;
        using Stella.Ergosfare.Core.Abstractions.Attributes;
        using Stella.Ergosfare.Events.Abstractions;
        namespace GroupCoverage;
        public sealed record Notice : IEvent;
        [Group("audit")]
        public sealed class Audit : IEventHandler<Notice>
        { public ValueTask HandleAsync(Notice message, ErgosfareContext context) => default; }
        public sealed class Default : IEventHandler<Notice>
        { public ValueTask HandleAsync(Notice message, ErgosfareContext context) => default; }
        public static class Caller
        {
            private static GroupSet Mutable = "audit";
            private static readonly GroupSet Uninitialized;
            private static readonly GroupSet Inner = "audit";
            private static readonly GroupSet Alias = Inner;
            private static GroupSet Build() => "audit";
            public static ValueTask Fire(IEventMediator mediator, string name)
                => mediator.PublishAsync(groups: {{expression}}, @event: ((new Notice() as Notice)!));
        }
        """;
}
