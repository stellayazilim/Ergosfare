namespace Stella.Ergosfare.SourceGenerator.Test;

[Trait("Category", "Unit")]
public class DispatchEvidenceCoverageTests
{
    [Theory]
    [InlineData("ICommandMediator", "SendAsync((ICommand)(object)value)", "Command", "Stella.Ergosfare.Commands.Abstractions.ICommand")]
    [InlineData("IQueryMediator", "QueryAsync((IQuery<int>)(object)value)", "Query", "Stella.Ergosfare.Queries.Abstractions.IQuery")]
    [InlineData("IEventMediator", "PublishAsync(value)", "Event", "Stella.Ergosfare.Events.Abstractions.IEvent")]
    public void UnconstrainedGenericDispatch_RecordsAnOpaqueMarker(string mediator, string call, string kind, string marker)
    {
        var result = GeneratorTestHost.Run($$"""
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Queries.Abstractions;
            using Stella.Ergosfare.Events.Abstractions;
            public static class Caller
            {
                public static void Fire<T>({{mediator}} mediator, T value) where T : notnull { _ = mediator.{{call}}; }
            }
            """);
        Assert.Empty(result.CompilationErrors);
        Assert.Contains("\"" + marker + "\", global::Stella.Ergosfare.Core.Abstractions.DispatchSites.DispatchKind." + kind + ", true)", result.GeneratedSource);
    }

    [Fact]
    public void ConditionalDispatchAndUnrelatedMethods_OnlyRecordMediatorCalls()
    {
        var result = GeneratorTestHost.Run("""
            using System;
            using Stella.Ergosfare.Events.Abstractions;
            public sealed record Notice : IEvent;
            public static class Caller
            {
                public static void PublishAsync() { }
                public static void Register() { }
                public static void Run(IEventMediator mediator, Action callback)
                {
                    _ = mediator?.PublishAsync(new Notice());
                    PublishAsync(); Register(); callback.Invoke();
                }
            }
            """);
        Assert.Empty(result.CompilationErrors);
        Assert.Contains("\"Notice\", global::Stella.Ergosfare.Core.Abstractions.DispatchSites.DispatchKind.Event, false)", result.GeneratedSource);
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Id == "CS8785");
    }
}
