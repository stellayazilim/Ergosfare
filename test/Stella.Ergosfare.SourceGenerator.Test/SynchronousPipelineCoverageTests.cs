using System.Reflection;

namespace Stella.Ergosfare.SourceGenerator.Test;

[Trait("Category", "Unit")]
public class SynchronousPipelineCoverageTests
{
    [Theory]
    [InlineData(false, false, "pre,main,post,final:12:False|12")]
    [InlineData(true, false, "pre,main,exception,final:41:True|41")]
    [InlineData(true, true, "pre,main,final:0:True|unhandled")]
    public async Task SyncStages_PreserveOrderResultsAndExceptionFilters(bool fail, bool unmatched, string expected)
    {
        // Explicit participant registrations isolate this fixture from other dynamically
        // loaded test assemblies' process-wide AddGenerated selection tables.
        var result = GeneratorTestHost.RunWithAllCandidates("""
            using System;
            using System.Collections.Generic;
            using System.Threading.Tasks;
            using Microsoft.Extensions.DependencyInjection;
            using Stella.Ergosfare.Commands.Abstractions;
            using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
            using Stella.Ergosfare.Core.Abstractions;
            using Stella.Ergosfare.Core.Abstractions.Handlers;
            using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
            namespace SyncCoverage;
            public sealed record Work(bool Fail, bool Unmatched) : ICommand<int>;
            public static class Entry
            {
                public static readonly List<string> Log = new();
                public static async Task<string> Run(bool fail, bool unmatched)
                {
                    await using var provider = new ServiceCollection()
                        .AddErgosfare(o => o.AddCommandModule(c => c.Register<Main>()
                            .Register<Pre>().Register<Post>().Register<Recover>().Register<Final>()))
                        .BuildServiceProvider();
                    try
                    {
                        var value = await provider.GetRequiredService<ICommandMediator>().SendAsync(new Work(fail, unmatched));
                        return string.Join(",", Log) + "|" + value;
                    }
                    catch (InvalidOperationException) when (Log.Contains("main"))
                    { return string.Join(",", Log) + "|unhandled"; }
                }
            }
            public sealed class Main : ICommandHandler<Work, int>
            {
                public ValueTask<int> HandleAsync(Work message, ErgosfareContext context)
                {
                    Entry.Log.Add("main");
                    if (message.Fail) throw message.Unmatched ? new InvalidOperationException() : new ArgumentException();
                    return new(10);
                }
            }
            public sealed class Pre : ICommand, IPreInterceptor<Work>
            {
                public object Handle(Work message, ErgosfareContext context) { Entry.Log.Add("pre"); return message; }
            }
            public sealed class Post : ICommand, IPostInterceptor<Work, int>
            {
                public object Handle(Work message, int result, ErgosfareContext context) { Entry.Log.Add("post"); return result + 2; }
            }
            public sealed class Recover : ICommand, IExceptionInterceptor<Work, int>, IExceptionInterceptorFilter<ArgumentException>
            {
                public object Handle(Work message, int result, Exception exception, ErgosfareContext context)
                { Entry.Log.Add("exception"); return 41; }
            }
            public sealed class Final : ICommand, IFinalInterceptor<Work, int>
            {
                public void Handle(Work message, int result, Exception exception, ErgosfareContext context)
                { Entry.Log.Add("final:" + result + ":" + (exception != null)); }
            }
            """);
        Assert.Empty(result.CompilationErrors);
        Assert.Empty(result.GeneratorDiagnostics);
        using var stream = new MemoryStream();
        var emitted = result.OutputCompilation.WithAssemblyName("SynchronousPipelineCoverageTests_" + Guid.NewGuid().ToString("N")).Emit(stream);
        Assert.True(emitted.Success, string.Join("\n", emitted.Diagnostics));
        var assembly = Assembly.Load(stream.ToArray());
        var run = assembly.GetType("SyncCoverage.Entry")!.GetMethod("Run")!;
        Assert.Equal(expected, await (Task<string>)run.Invoke(null, [fail, unmatched])!);
    }
}
