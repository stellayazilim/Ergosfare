using System.Reflection;

namespace Stella.Ergosfare.SourceGenerator.Test;

[Trait("Category", "Unit")]
public class ForeignCarrierCoverageTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    public async Task ReferenceCarrier_PostFailuresAndFilteredThrowsKeepTheirChannels(bool materializes, bool postFailure, bool bare)
    {
        var source = $$"""
            using System;
            using System.Threading.Tasks;
            using Microsoft.Extensions.DependencyInjection;
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Attributes;
            using Stella.Ergosfare.Core.Abstractions.Results;
            using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
            using Stella.Ergosfare.Plugins.Abstractions;
            namespace CarrierCoverage;
            public sealed record Outcome(Exception Error);
            public sealed class Adapter : IResultAdapter<Outcome>{{(materializes ? ", IResultMaterializer<Outcome>" : "")}}
            {
                public bool TryGetException(in Outcome result, out Exception exception)
                { exception = result.Error; return exception != null; }
                {{(materializes ? "public Outcome Materialize(Exception exception) => new(exception);" : "")}}
            }
            [ResultAdapter(typeof(Adapter))]
            public sealed record Work(bool PostFailure) : ICommand<Outcome>;
            public sealed class Main : ICommandHandler<Work, Outcome>
            {
                public ValueTask<Outcome> HandleAsync(Work message, ErgosfareContext context)
                {
                    if (!message.PostFailure) throw new InvalidOperationException("handler");
                    return new(new Outcome(null));
                }
            }
            public sealed class Hooks
            {
                public static int Finished;
                [PipelineInvokable(Hook.Finish)]
                public static void Finish<T>(T message, ErgosfareContext context) { Finished++; }
            }
            public static class Entry
            {
                public static async Task<string> Run(bool postFailure)
                {
                    await using var provider = new ServiceCollection()
                        .AddErgosfare(o => o.AddCommandModule(c => c.AddGenerated())).BuildServiceProvider();
                    try
                    {
                        var result = await provider.GetRequiredService<ICommandMediator>().SendAsync(new Work(postFailure));
                        return "result:" + result.Error?.Message;
                    }
                    catch (InvalidOperationException e) { return "throw:" + e.Message; }
                }
            }
            """;
        if (!bare) source += """

            public sealed class Post : ICommandPostInterceptor<Work, Outcome>
            {
                public ValueTask<Outcome> HandleAsync(Work message, Outcome result, ErgosfareContext context)
                    => new(new Outcome(new ArgumentException("post")));
            }
            public sealed class Recover : ICommandExceptionInterceptorFor<Work, Outcome, ArgumentException>
            {
                public ValueTask<Outcome> HandleAsync(Work message, Outcome result, ArgumentException exception, ErgosfareContext context)
                    => new(new Outcome(new ArgumentException("recovered")));
            }
            public sealed class Final : ICommandFinalInterceptor<Work>
            {
                public ValueTask HandleAsync(Work message, object result, Exception exception, ErgosfareContext context) => default;
            }
            """;
        var result = GeneratorTestHost.RunWithAllCandidates(source);
        Assert.Empty(result.CompilationErrors);
        Assert.Empty(result.GeneratorDiagnostics);
        using var stream = new MemoryStream();
        var emitted = result.OutputCompilation.WithAssemblyName("ForeignCarrierCoverageTests_" + Guid.NewGuid().ToString("N")).Emit(stream);
        Assert.True(emitted.Success, string.Join("\n", emitted.Diagnostics));
        var assembly = Assembly.Load(stream.ToArray());
        var run = assembly.GetType("CarrierCoverage.Entry")!.GetMethod("Run")!;
        var expected = postFailure ? "result:recovered" : materializes ? "result:handler" : "throw:handler";
        Assert.Equal(expected, await (Task<string>)run.Invoke(null, [postFailure])!);
        // Finish is on the normal stage path; a thrown handler exits to exception handling first.
        Assert.Equal(postFailure ? 1 : 0, assembly.GetType("CarrierCoverage.Hooks")!.GetField("Finished")!.GetValue(null));
    }
}
