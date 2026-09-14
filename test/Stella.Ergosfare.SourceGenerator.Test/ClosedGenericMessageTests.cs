using System.Reflection;
using Microsoft.CodeAnalysis;

namespace Stella.Ergosfare.SourceGenerator.Test;

public class ClosedGenericMessageTests
{
    [Fact]
    public void Group_calls_for_closed_messages_do_not_share_plan_keys()
    {
        var result = GeneratorTestHost.RunWithAllCandidates("""
            using System.Threading.Tasks;
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Attributes;
            namespace ClosedGroups {
                public sealed class Message<T> : ICommand { }
                [Group("ints")]
                public sealed class IntHandler : ICommandHandler<Message<int>> {
                    public ValueTask HandleAsync(Message<int> message, ErgosfareContext context) => default;
                }
                [Group("strings")]
                public sealed class StringHandler : ICommandHandler<Message<string>> {
                    public ValueTask HandleAsync(Message<string> message, ErgosfareContext context) => default;
                }
                public static class App {
                    public static async Task Run(ICommandMediator mediator) {
                        await mediator.SendAsync(new Message<int>(), "ints");
                        await mediator.SendAsync(new Message<string>(), "strings");
                    }
                }
            }
            """);
        Assert.Empty(result.CompilationErrors);
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("StagedPlan<global::ClosedGroups.Message<int>", result.GeneratedSource);
        Assert.Contains("StagedPlan<global::ClosedGroups.Message<string>", result.GeneratedSource);
    }

    [Theory]
    [InlineData("builder.Register<Handler<int>>();", false)]
    [InlineData("builder.Register(typeof(Handler<>));", true)]
    [InlineData("builder.AddGenerated();", true)]
    public void Closed_selection_keeps_one_instantiation_and_open_selection_keeps_all_known_forms(string selection, bool both)
    {
        var result = GeneratorTestHost.Run($$"""
            using System.Threading.Tasks;
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
            using Stella.Ergosfare.Core.Abstractions;
            namespace GenericSelections {
                public sealed class Message<T> : ICommand<string> { }
                public sealed class Handler<T> : ICommandHandler<Message<T>, string> {
                    public ValueTask<string> HandleAsync(Message<T> message, ErgosfareContext context) => new(typeof(T).Name);
                }
                public static class App {
                    // Both closed participants are visible, but visibility does not select them.
                    public static System.Type[] Known = { typeof(Handler<int>), typeof(Handler<string>) };
                    public static void Configure(CommandModuleBuilder builder) { {{selection}} }
                }
            }
            """);
        Assert.Empty(result.CompilationErrors);
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("StagedPlan<global::GenericSelections.Message<int>", result.GeneratedSource);
        if (both) Assert.Contains("StagedPlan<global::GenericSelections.Message<string>", result.GeneratedSource);
        else Assert.DoesNotContain("StagedPlan<global::GenericSelections.Message<string>", result.GeneratedSource);
    }

    private const string Participants = """
        using System.Threading.Tasks;
        using Stella.Ergosfare.Commands.Abstractions;
        using Stella.Ergosfare.Core.Abstractions;
        using Stella.Ergosfare.Core.Abstractions.Attributes;
        namespace ClosedMessages {
        public sealed record WrappedProbe<T>(T Value) : ICommand<string>;
        [DiscoveryKey("selected")]
        public sealed class IntHandler : ICommandHandler<WrappedProbe<int>, string> {
            public ValueTask<string> HandleAsync(WrappedProbe<int> message, ErgosfareContext context)
                => new("int:" + message.Value);
        }
        [DiscoveryKey("selected")]
        public sealed class StringHandler : ICommandHandler<WrappedProbe<string>, string> {
            public ValueTask<string> HandleAsync(WrappedProbe<string> message, ErgosfareContext context)
                => new("string:" + message.Value);
        }
        [DiscoveryKey("unselected")]
        public sealed class DoubleHandler : ICommandHandler<WrappedProbe<double>, string> {
            public ValueTask<string> HandleAsync(WrappedProbe<double> message, ErgosfareContext context)
                => new("double");
        }
        }
        """;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Selected_handlers_produce_distinct_executable_closed_message_plans(bool referenced)
    {
        var result = GeneratorTestHost.Run((referenced ? "" : Participants) + """

            namespace ClosedMessages {
                using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
                public static class App {
                    public static async System.Threading.Tasks.Task<string[]> Run() {
                        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
                        Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection.ServiceCollectionExtensions.AddErgosfare(
                            services, options => options.AddCommandModule(commands => commands.AddGenerated("selected")));
                        using var provider = Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(services);
                        var mediator = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Stella.Ergosfare.Commands.Abstractions.ICommandMediator>(provider);
                        return new[] {
                            await mediator.SendAsync<string>(new WrappedProbe<int>(42)),
                            await mediator.SendAsync<string>(new WrappedProbe<string>("ok"))
                        };
                    }
                }
            }
            """, libraries: referenced ? [("ClosedMessageLibrary", Participants)] : null);

        Assert.Empty(result.CompilationErrors);
        Assert.DoesNotContain(result.GeneratorDiagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("WrappedProbe<int>", result.GeneratedSource);
        Assert.Contains("WrappedProbe<string>", result.GeneratedSource);
        Assert.DoesNotContain("WrappedProbe<double>", result.GeneratedSource);
        using var stream = new MemoryStream();
        var emit = result.OutputCompilation.Emit(stream);
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
        var assembly = Assembly.Load(stream.ToArray());
        var run = assembly.GetType("ClosedMessages.App")!.GetMethod("Run")!;
        var values = await (Task<string[]>)run.Invoke(null, null)!;
        Assert.Equal(["int:42", "string:ok"], values);
    }
}
