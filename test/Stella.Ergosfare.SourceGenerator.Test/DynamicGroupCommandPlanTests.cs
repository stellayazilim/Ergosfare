using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Planning;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

namespace Stella.Ergosfare.SourceGenerator.Test;

public class DynamicGroupCommandPlanTests
{
    [Fact]
    public async Task FullPlan_SelectsOneHandler_AndRejectsMissingOrAmbiguousGroups()
    {
        var result = GeneratorTestHost.RunWithAllCandidates("""
            using System.Threading.Tasks;
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Attributes;
            namespace DynamicGroups
            {
                public sealed class Request : ICommand<string> { }
                [Group("red")]
                public sealed class Red : ICommandHandler<Request, string>
                {
                    public ValueTask<string> HandleAsync(Request message, ErgosfareContext context) => new("red");
                }
                [Group("blue")]
                public sealed class Blue : ICommandHandler<Request, string>
                {
                    public ValueTask<string> HandleAsync(Request message, ErgosfareContext context) => new("blue");
                }
                public static class Caller
                {
                    public static ValueTask<string> Run(ICommandMediator mediator, Request message, GroupSet groups)
                        => mediator.SendAsync<string>(message, groups, System.Threading.CancellationToken.None);
                }
            }
            """);
        Assert.Empty(result.CompilationErrors);
        Assert.Contains("AddFilteredPlan<global::DynamicGroups.Request, string>", result.GeneratedSource);
        Assert.Contains("new global::DynamicGroups.Red()", result.GeneratedSource);
        Assert.Contains("new global::DynamicGroups.Blue()", result.GeneratedSource);
        Assert.DoesNotContain("ExecuteDirect(", result.GeneratedSource);
        using var output = new MemoryStream();
        Assert.True(result.OutputCompilation.Emit(output).Success);
        var assembly = Assembly.Load(output.ToArray());
        var registrations = assembly.GetType("Stella.Ergosfare.Generated.ErgosfareGeneratedRegistrations")!;
        var register = GeneratorTestHost.SelectionFor(registrations, typeof(CommandModuleBuilder));
        await using var provider = new ServiceCollection()
            .AddErgosfare(r => r.AddCommandModule(m => register.Invoke(null, [m])))
            .BuildServiceProvider();
        var messageType = assembly.GetType("DynamicGroups.Request")!;
        var message = Activator.CreateInstance(messageType)!;
        var engine = provider.GetRequiredService<MessageDispatchEngine>();
        var context = new ErgosfareContext();
        Assert.Equal("red", await engine.DispatchAsync<string>(message, context, provider, ["red"]));
        Assert.Equal("blue", await engine.DispatchAsync<string>(message, context, provider, ["blue", "blue"]));
        await Assert.ThrowsAsync<NoHandlerFoundException>(async () =>
            await engine.DispatchAsync<string>(message, context, provider, ["missing"]));
        await Assert.ThrowsAsync<MultipleHandlerFoundException>(async () =>
            await engine.DispatchAsync<string>(message, context, provider, ["red", "blue"]));
        var plan = GeneratedPlanRegistry.FindFilteredResultPlan(messageType, typeof(string));
        Assert.Equal(2, plan!.Composition.HandlerTypes.Count);
        Assert.IsAssignableFrom<IPipelineExecutor<string>>(plan);
    }
}
