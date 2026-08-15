using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

namespace Stella.Ergosfare.SourceGenerator.Test;

/// <summary>
///     A message with a handler on one of its base contracts — the documented cross-cutting
///     idiom — executed end to end. The priority ladder gives the message to its direct
///     handler and keeps the covariant one as a fallback, and the compiled plan has to say
///     exactly that.
/// </summary>
/// <remarks>
///     The delivery assertion alone would pass with no plan at all: the runtime lane resolves
///     this correctly and always did. The proof that the <em>plan</em> served the dispatch is
///     the plugin hook — the core runtime knows nothing about plugins, so a fired hook can
///     only have come from a plan body the gate admitted.
/// </remarks>
public class CovariantSiblingPlanExecutionTests
{
    private const string Source = """
        using System.Collections.Generic;
        using System.Threading.Tasks;
        using Stella.Ergosfare.Commands.Abstractions;
        using Stella.Ergosfare.Core.Abstractions;
        using Stella.Ergosfare.Plugins.Abstractions;

        namespace TestApp
        {
            public static class LadderSink
            {
                public static readonly List<string> Entries = new List<string>();
            }

            public interface IGenAudited : ICommand { }

            public sealed class GenTransferMoney : ICommand, IGenAudited { }

            public sealed class GenTransferMoneyHandler : ICommandHandler<GenTransferMoney>
            {
                public ValueTask HandleAsync(GenTransferMoney command, ErgosfareContext context)
                {
                    LadderSink.Entries.Add("direct");
                    return default;
                }
            }

            public sealed class GenAuditedHandler : ICommandHandler<IGenAudited>
            {
                public ValueTask HandleAsync(IGenAudited command, ErgosfareContext context)
                {
                    LadderSink.Entries.Add("covariant");
                    return default;
                }
            }

            public sealed class GenTransferPre : ICommandPreInterceptor<GenTransferMoney>
            {
                public ValueTask<GenTransferMoney> HandleAsync(GenTransferMoney command, ErgosfareContext context)
                {
                    LadderSink.Entries.Add("pre");
                    return ValueTask.FromResult(command);
                }
            }

            public sealed class GenLadderHooks
            {
                [PipelineInvokable(Hook.Finish)]
                public void Finished<TMessage>(TMessage message, ErgosfareContext context)
                    => LadderSink.Entries.Add("plan");
            }
        }
        """;

    private static readonly Lazy<(Assembly Assembly, ServiceProvider Provider)> Host = new(() =>
    {
        var result = GeneratorTestHost.Run(Source);

        Assert.Empty(result.CompilationErrors);

        using var stream = new MemoryStream();
        Assert.True(result.OutputCompilation.Emit(stream).Success);

        var assembly = Assembly.Load(stream.ToArray());
        var registrations = assembly.GetType(
            "Stella.Ergosfare.Generated.ErgosfareGeneratedRegistrations", throwOnError: true)!;
        var registerCommands = registrations.GetMethod("RegisterGenerated", [typeof(CommandModuleBuilder)])!;

        var services = new ServiceCollection();
        services.AddSingleton(assembly.GetType("TestApp.GenLadderHooks", throwOnError: true)!);

        var provider = services
            .AddErgosfare(options => options.AddCommandModule(commands => registerCommands.Invoke(null, [commands])))
            .BuildServiceProvider();

        return (assembly, provider);
    });

    private static List<string> Entries
        => (List<string>)Host.Value.Assembly.GetType("TestApp.LadderSink", throwOnError: true)!
            .GetField("Entries", BindingFlags.Public | BindingFlags.Static)!
            .GetValue(null)!;

    [Fact]
    [Trait("Category", "Unit")]
    public async Task TheDirectHandlerServesTheMessage_AndThePlanIsWhatRanIt()
    {
        var (assembly, provider) = Host.Value;

        Entries.Clear();

        await provider.GetRequiredService<ICommandMediator>().SendAsync(
            (ICommand)Activator.CreateInstance(assembly.GetType("TestApp.GenTransferMoney", throwOnError: true)!)!);

        // "covariant" is absent because the direct level wins outright, and "plan" is
        // present because only a plan body carries a plugin call.
        Assert.Equal(["pre", "direct", "plan"], Entries);
    }
}
