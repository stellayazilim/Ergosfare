// Stream messaging is under revision and its entry points carry the notice; these are
// deliberate call sites of the surface as it stands today.
#pragma warning disable CS0618

using System.Reflection;
using Stella.Ergosfare.Core.Abstractions.DispatchRoots;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Queries.Abstractions;
using Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.SourceGenerator.Test;

/// <summary>
/// Strategy-parity matrix for the emitted stream plans, executed end to end: the app
/// source below compiles with the generator, the emitted assembly loads into the test
/// process, its <c>RegisterGenerated</c> wires a real container, and streams run through
/// the public query mediator. Covered: query rewrite by pre-interceptors with the items
/// flowing to the caller, the post and final stages observing the enumerator after
/// enumeration, a mid-stream failure swallowed by the exception stage after the items
/// that preceded it, and <c>ExecutionAbortedException</c> skipping every later stage.
/// </summary>
public class StreamPlanExecutionTests
{
    private const string Source = """
        #pragma warning disable CS0618
        using System;
        using System.Collections.Generic;
        using System.Threading.Tasks;
        using Stella.Ergosfare.Core.Abstractions;
        using Stella.Ergosfare.Core.Abstractions.Handlers;
        using Stella.Ergosfare.Queries.Abstractions;

        namespace StreamTestApp
        {
            public static class Sink
            {
                public static readonly List<string> Entries = new List<string>();
            }

            // --- happy path: rewrite + stage order -------------------------------

            public sealed class GenTickStream : IStreamQuery<int>
            {
                public int Seed { get; set; } = 1;
            }

            public sealed class GenTickStreamHandler : IStreamQueryHandler<GenTickStream, int>
            {
                public async IAsyncEnumerable<int> StreamAsync(GenTickStream query, ErgosfareContext context)
                {
                    Sink.Entries.Add("handler:" + query.Seed);
                    yield return query.Seed;
                    await Task.Yield();
                    yield return query.Seed + 1;
                }
            }

            public sealed class GenTickStreamPre : IQueryPreInterceptor<GenTickStream>
            {
                public ValueTask<GenTickStream> HandleAsync(GenTickStream query, ErgosfareContext context)
                {
                    Sink.Entries.Add("pre:" + query.Seed);
                    return ValueTask.FromResult(new GenTickStream { Seed = 10 });
                }
            }

            // The typed stream-stage contracts have no flavored aliases, so the module
            // marker rides along explicitly.
            public sealed class GenTickStreamPost : IQuery, IAsyncPostInterceptor<GenTickStream, IAsyncEnumerator<int>>
            {
                public ValueTask<object> HandleAsync(GenTickStream query, IAsyncEnumerator<int> messageResult, ErgosfareContext context)
                {
                    Sink.Entries.Add("post:" + (messageResult != null ? "enumerator" : "null"));
                    return ValueTask.FromResult<object>(messageResult!);
                }
            }

            public sealed class GenTickStreamFinal : IQuery, IAsyncFinalInterceptor<GenTickStream>
            {
                public ValueTask HandleAsync(GenTickStream query, object? messageResult, Exception? exception, ErgosfareContext context)
                {
                    Sink.Entries.Add("final:" + (exception == null ? "clean" : exception.Message));
                    return default;
                }
            }

            // --- mid-stream failure swallowed by the exception stage -------------

            public sealed class GenFailStream : IStreamQuery<int>;

            public sealed class GenFailStreamHandler : IStreamQueryHandler<GenFailStream, int>
            {
                public async IAsyncEnumerable<int> StreamAsync(GenFailStream query, ErgosfareContext context)
                {
                    yield return 1;
                    await Task.Yield();
                    yield return 2;
                    throw new InvalidOperationException("mid-boom");
                }
            }

            public sealed class GenFailStreamExceptionInterceptor : IQuery, IAsyncExceptionInterceptor<GenFailStream>
            {
                public ValueTask<object> HandleAsync(GenFailStream query, object? messageResult, Exception exception, ErgosfareContext context)
                {
                    Sink.Entries.Add("exception:" + exception.Message);
                    return ValueTask.FromResult<object>(messageResult!);
                }
            }

            public sealed class GenFailStreamFinal : IQuery, IAsyncFinalInterceptor<GenFailStream>
            {
                public ValueTask HandleAsync(GenFailStream query, object? messageResult, Exception? exception, ErgosfareContext context)
                {
                    Sink.Entries.Add("final:" + (exception == null ? "clean" : exception.Message));
                    return default;
                }
            }

            // --- abort: nothing later runs, the final stage included -------------

            public sealed class GenAbortStream : IStreamQuery<int>;

            public sealed class GenAbortStreamHandler : IStreamQueryHandler<GenAbortStream, int>
            {
                public async IAsyncEnumerable<int> StreamAsync(GenAbortStream query, ErgosfareContext context)
                {
                    Sink.Entries.Add("handler");
                    await Task.Yield();
                    yield return 1;
                }
            }

            public sealed class GenAbortStreamPre : IQueryPreInterceptor<GenAbortStream>
            {
                public ValueTask<GenAbortStream> HandleAsync(GenAbortStream query, ErgosfareContext context)
                {
                    Sink.Entries.Add("pre");
                    context.Abort();
                    return ValueTask.FromResult(query);
                }
            }

            public sealed class GenAbortStreamExceptionInterceptor : IQuery, IAsyncExceptionInterceptor<GenAbortStream>
            {
                public ValueTask<object> HandleAsync(GenAbortStream query, object? messageResult, Exception exception, ErgosfareContext context)
                {
                    Sink.Entries.Add("exception");
                    return ValueTask.FromResult<object>(messageResult!);
                }
            }

            public sealed class GenAbortStreamFinal : IQuery, IAsyncFinalInterceptor<GenAbortStream>
            {
                public ValueTask HandleAsync(GenAbortStream query, object? messageResult, Exception? exception, ErgosfareContext context)
                {
                    Sink.Entries.Add("final");
                    return default;
                }
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
        var registrations = assembly.GetType("Stella.Ergosfare.Generated.ErgosfareGeneratedRegistrations", throwOnError: true)!;
        var registerQueries = registrations.GetMethod("RegisterGenerated", [typeof(QueryModuleBuilder)])!;

        var provider = new ServiceCollection()
            .AddErgosfare(options => options.AddQueryModule(queries => registerQueries.Invoke(null, [queries])))
            .BuildServiceProvider();

        return (assembly, provider);
    });

    private static List<string> Entries
        => (List<string>)Host.Value.Assembly.GetType("StreamTestApp.Sink", throwOnError: true)!
            .GetField("Entries", BindingFlags.Public | BindingFlags.Static)!
            .GetValue(null)!;

    private static IStreamQuery<int> CreateQuery(string typeName)
        => (IStreamQuery<int>)Activator.CreateInstance(Host.Value.Assembly.GetType(typeName, throwOnError: true)!)!;

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task HappyPath_RewritesTheQueryAndRunsStagesAroundTheEnumeration()
    {
        var (assembly, provider) = Host.Value;

        Assert.NotNull(GeneratedDispatchRoots.FindStagedStreamPlan(
            assembly.GetType("StreamTestApp.GenTickStream")!, typeof(int)));

        Entries.Clear();
        var items = new List<int>();

        await foreach (var item in provider.GetRequiredService<IQueryMediator>()
                           .StreamAsync(CreateQuery("StreamTestApp.GenTickStream")))
        {
            items.Add(item);
        }

        // The handler streams from the pre-interceptor's rewritten instance, the post
        // stage observes the enumerator once enumeration ends, and the final stage sees a
        // clean outcome last — the strategy's exact order, baked.
        Assert.Equal([10, 11], items);
        Assert.Equal(["pre:1", "handler:10", "post:enumerator", "final:clean"], Entries);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task MidStreamFailure_RunsTheExceptionStageAndEndsTheStreamQuietly()
    {
        var (assembly, provider) = Host.Value;

        Assert.NotNull(GeneratedDispatchRoots.FindStagedStreamPlan(
            assembly.GetType("StreamTestApp.GenFailStream")!, typeof(int)));

        Entries.Clear();
        var items = new List<int>();

        // The items before the failure reach the caller; the exception stage accepts the
        // failure, so nothing is thrown, and the final stage still observes it.
        await foreach (var item in provider.GetRequiredService<IQueryMediator>()
                           .StreamAsync(CreateQuery("StreamTestApp.GenFailStream")))
        {
            items.Add(item);
        }

        Assert.Equal([1, 2], items);
        Assert.Equal(["exception:mid-boom", "final:mid-boom"], Entries);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task Abort_CutsThePlanAndReachesTheEnumeratingCaller()
    {
        var (assembly, provider) = Host.Value;

        Assert.NotNull(GeneratedDispatchRoots.FindStagedStreamPlan(
            assembly.GetType("StreamTestApp.GenAbortStream")!, typeof(int)));

        Entries.Clear();

        // The emitted plan stops where the strategy would: the handler never runs, the
        // exception stage never sees the abort, the final stage does not run either, and
        // the signal reaches whoever is enumerating.
        await Assert.ThrowsAsync<ExecutionAbortedException>(async () =>
        {
            await foreach (var _ in provider.GetRequiredService<IQueryMediator>()
                               .StreamAsync(CreateQuery("StreamTestApp.GenAbortStream")))
            {
            }
        });

        Assert.Equal(["pre"], Entries);
    }
}
