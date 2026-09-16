using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Core.Abstractions.Planning;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Events.Abstractions;
using Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection;

namespace Stella.Ergosfare.SourceGenerator.Test;

public class EmptyStringGroupTests
{
    private static string Source(bool matchingHandler, bool dynamicGroups, bool singleString) => $$"""
        using System.Threading.Tasks;
        using Stella.Ergosfare.Core.Abstractions;
        using Stella.Ergosfare.Core.Abstractions.Attributes;
        using Stella.Ergosfare.Events.Abstractions;
        namespace EmptyGroupProbe
        {
            public sealed class Notice : IEvent { }
            [Group("{{(matchingHandler ? "" : "named")}}")]
            public sealed class Subscriber : IEventHandler<Notice>
            {
                public static int Calls;
                public ValueTask HandleAsync(Notice message, ErgosfareContext context)
                {
                    Calls++;
                    return default;
                }
            }
            public static class Caller
            {
                public static ValueTask Publish(IEventMediator mediator, Notice message, {{(singleString ? "string" : "Stella.Ergosfare.Core.Abstractions.GroupSet")}} groups)
                    => mediator.PublishAsync(message, {{(dynamicGroups ? "groups" : singleString ? "\"\"" : "[\"\"]")}}, System.Threading.CancellationToken.None);
            }
        }
        """;

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task EmptyString_IsAGroupName_AndRequiresAMatchingHandler(bool matchingHandler, bool dynamicGroups, bool singleString)
    {
        var result = GeneratorTestHost.RunWithAllCandidates(Source(matchingHandler, dynamicGroups, singleString),
            buildProperties: new Dictionary<string, string> { ["ErgosfareCompositionRoot"] = "true" });
        Assert.Empty(result.CompilationErrors);
        var syntaxTree = result.OutputCompilation.SyntaxTrees.First();
        var call = syntaxTree.GetRoot().DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax>().Single();
        var method = (IMethodSymbol)result.OutputCompilation.GetSemanticModel(syntaxTree).GetSymbolInfo(call).Symbol!;
        Assert.Equal("Stella.Ergosfare.Core.Abstractions.GroupSet", method.Parameters[1].Type.ToDisplayString().TrimEnd('?'));

        if (!dynamicGroups && !matchingHandler)
        {
            var diagnostic = Assert.Single(result.GeneratorDiagnostics);
            Assert.Equal("ERGO024", diagnostic.Id);
            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
            return;
        }

        Assert.Empty(result.GeneratorDiagnostics);
        using var stream = new MemoryStream();
        Assert.True(result.OutputCompilation.Emit(stream).Success);
        var assembly = Assembly.Load(stream.ToArray());
        var eventType = assembly.GetType("EmptyGroupProbe.Notice", true)!;
        var registration = GeneratorTestHost.SelectionFor(
            assembly.GetType("Stella.Ergosfare.Generated.ErgosfareGeneratedRegistrations", true)!, typeof(EventModuleBuilder));
        await using var provider = new ServiceCollection()
            .AddErgosfare(options => options.AddEventModule(events => registration.Invoke(null, [events])))
            .BuildServiceProvider();
        if (dynamicGroups)
            Assert.NotNull(GeneratedPlanRegistry.FindFilteredBroadcastPlan(eventType));
        else
            Assert.NotNull(GeneratedPlanRegistry.FindBroadcastPlan(eventType, [""]));
        var mediator = provider.GetRequiredService<IEventMediator>();
        var message = (IEvent)Activator.CreateInstance(eventType)!;
        ValueTask Publish() => singleString
            ? mediator.PublishAsync(message, "", CancellationToken.None)
            : mediator.PublishAsync(message, [""], CancellationToken.None);
        if (matchingHandler)
            await Publish();
        else
            await Assert.ThrowsAsync<NoHandlerFoundException>(
                async () => await Publish());

        Assert.Equal(matchingHandler ? 1 : 0,
            (int)assembly.GetType("EmptyGroupProbe.Subscriber", true)!.GetField("Calls")!.GetValue(null)!);

        // A request with no group names means "default", not the empty-string group.
        await Assert.ThrowsAsync<NoHandlerFoundException>(async () => await mediator.PublishAsync(message));
    }
}
