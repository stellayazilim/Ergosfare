using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.DispatchRoots;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

namespace Stella.Ergosfare.SourceGenerator.Test;

/// <summary>
///     The canonical use of groups on a single-handler pipeline: one message, one handler per
///     group. Each set selects exactly one of them, so the message is unambiguous under every
///     set — and each set is entitled to its own compiled plan.
/// </summary>
/// <remarks>
///     Both halves are asserted, because the delivery half alone would pass with no plan at
///     all: the runtime group lane dispatches this correctly and always did. The plan store
///     is what says the compiled lane serves it.
/// </remarks>
public class GroupedCommandPlanExecutionTests
{
    private const string Source = """
        using System.Collections.Generic;
        using System.Threading.Tasks;
        using Stella.Ergosfare.Commands.Abstractions;
        using Stella.Ergosfare.Core.Abstractions;
        using Stella.Ergosfare.Core.Abstractions.Attributes;

        namespace TestApp
        {
            public static class ArchiveSink
            {
                public static readonly List<string> Entries = new List<string>();
            }

            public sealed class GenArchiveTodo : ICommand { }

            [Group("audit")]
            public sealed class GenAuditArchiveHandler : ICommandHandler<GenArchiveTodo>
            {
                public ValueTask HandleAsync(GenArchiveTodo command, ErgosfareContext context)
                {
                    ArchiveSink.Entries.Add("audit");
                    return default;
                }
            }

            [Group("billing")]
            public sealed class GenBillingArchiveHandler : ICommandHandler<GenArchiveTodo>
            {
                public ValueTask HandleAsync(GenArchiveTodo command, ErgosfareContext context)
                {
                    ArchiveSink.Entries.Add("billing");
                    return default;
                }
            }

            public static class ArchiveCaller
            {
                public static readonly GroupSet Audit = GroupSet.Of("audit");
                public static readonly GroupSet Billing = GroupSet.Of("billing");

                public static ValueTask SendAudit(ICommandMediator mediator, GenArchiveTodo command)
                    => mediator.SendAsync(command, Audit);

                public static ValueTask SendBilling(ICommandMediator mediator, GenArchiveTodo command)
                    => mediator.SendAsync(command, Billing);
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

        var provider = new ServiceCollection()
            .AddErgosfare(options => options.AddCommandModule(commands => registerCommands.Invoke(null, [commands])))
            .BuildServiceProvider();

        return (assembly, provider);
    });

    private static List<string> Entries
        => (List<string>)Host.Value.Assembly.GetType("TestApp.ArchiveSink", throwOnError: true)!
            .GetField("Entries", BindingFlags.Public | BindingFlags.Static)!
            .GetValue(null)!;

    /// <summary>
    ///     Each proven set holds its own plan, and neither is the default one — the default
    ///     group selects no handler here, so its pipeline is empty and nothing is baked for it.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void EachGroupSet_HasItsOwnPlanInTheStore()
    {
        var (assembly, _) = Host.Value;
        var commandType = assembly.GetType("TestApp.GenArchiveTodo", throwOnError: true)!;

        var audit = GeneratedDispatchRoots.FindStagedVoidPlan(commandType, ["audit"]);
        var billing = GeneratedDispatchRoots.FindStagedVoidPlan(commandType, ["billing"]);

        Assert.NotNull(audit);
        Assert.NotNull(billing);
        Assert.NotSame(audit, billing);
        Assert.Null(GeneratedDispatchRoots.FindStagedVoidPlan(commandType));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task FilteredSend_ReachesTheHandlerOfThatGroupAlone()
    {
        var (assembly, provider) = Host.Value;
        var mediator = provider.GetRequiredService<ICommandMediator>();
        var commandType = assembly.GetType("TestApp.GenArchiveTodo", throwOnError: true)!;

        Entries.Clear();

        await mediator.SendAsync((ICommand)Activator.CreateInstance(commandType)!, GroupSet.Of("audit"));
        await mediator.SendAsync((ICommand)Activator.CreateInstance(commandType)!, GroupSet.Of("billing"));

        Assert.Equal(["audit", "billing"], Entries);
    }
}
