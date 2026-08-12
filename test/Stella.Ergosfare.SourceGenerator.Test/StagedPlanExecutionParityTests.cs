using System.Reflection;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Abstractions.DispatchRoots;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Queries.Abstractions;
using Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.SourceGenerator.Test;

/// <summary>
/// Strategy-parity matrix for the emitted staged plans, executed end to end: the app
/// source below compiles with the generator, the emitted assembly loads into the test
/// process (it shares the real Ergosfare assemblies), its <c>RegisterGenerated</c> wires a
/// real container, and dispatches run through the public mediators. Covered: message
/// rewrite by pre-interceptors, execution order across stages, post result rewrite,
/// exception capture with strategy-identical swallowing, propagation without exception
/// interceptors, <c>ExecutionAbortedException</c> skipping the exception stage while
/// finals still run, and finals observing the outcome. Participants log into a static
/// sink inside the emitted assembly, read back reflectively.
/// </summary>
public class StagedPlanExecutionParityTests
{
    private const string Source = """
        using System;
        using System.Collections.Generic;
        using System.Threading.Tasks;
        using Stella.Ergosfare.Commands.Abstractions;
        using Stella.Ergosfare.Core.Abstractions;
        using Stella.Ergosfare.Queries.Abstractions;

        namespace TestApp
        {
            public static class Sink
            {
                public static readonly List<string> Entries = new List<string>();
            }

            // --- happy path: rewrite + order -------------------------------------

            public sealed class GenHappyCommand : ICommand
            {
                public string Tag { get; set; } = "original";
            }

            public sealed class GenHappyCommandHandler : ICommandHandler<GenHappyCommand>
            {
                public ValueTask HandleAsync(GenHappyCommand command, ErgosfareContext context)
                {
                    Sink.Entries.Add("handler:" + command.Tag);
                    return default;
                }
            }

            public sealed class GenHappyCommandPre : ICommandPreInterceptor<GenHappyCommand>
            {
                public ValueTask<GenHappyCommand> HandleAsync(GenHappyCommand command, ErgosfareContext context)
                {
                    Sink.Entries.Add("pre:" + command.Tag);
                    return ValueTask.FromResult(new GenHappyCommand { Tag = "rewritten" });
                }
            }

            public sealed class GenHappyCommandPost : ICommandPostInterceptor<GenHappyCommand>
            {
                public ValueTask<object> HandleAsync(GenHappyCommand command, object messageResult, ErgosfareContext context)
                {
                    Sink.Entries.Add("post:" + command.Tag);
                    return ValueTask.FromResult(messageResult);
                }
            }

            public sealed class GenHappyCommandFinal : ICommandFinalInterceptor<GenHappyCommand>
            {
                public ValueTask HandleAsync(GenHappyCommand command, object? messageResult, Exception? exception, ErgosfareContext context)
                {
                    Sink.Entries.Add("final:" + (exception == null ? "clean" : exception.Message));
                    return default;
                }
            }

            // --- void exception swallow: the flavored exception interceptor's
            // result-agnostic contract works on void pipelines too -------------------

            public sealed class GenVoidSwallowCommand : ICommand { }

            public sealed class GenVoidSwallowCommandHandler : ICommandHandler<GenVoidSwallowCommand>
            {
                public ValueTask HandleAsync(GenVoidSwallowCommand command, ErgosfareContext context)
                {
                    Sink.Entries.Add("handler");
                    throw new InvalidOperationException("void-boom");
                }
            }

            public sealed class GenVoidSwallowCommandExceptionInterceptor : ICommandExceptionInterceptor<GenVoidSwallowCommand>
            {
                public ValueTask<object> HandleAsync(GenVoidSwallowCommand command, object? messageResult, Exception exception, ErgosfareContext context)
                {
                    Sink.Entries.Add("exception:" + exception.Message);
                    return ValueTask.FromResult<object>(messageResult!);
                }
            }

            public sealed class GenVoidSwallowCommandFinal : ICommandFinalInterceptor<GenVoidSwallowCommand>
            {
                public ValueTask HandleAsync(GenVoidSwallowCommand command, object? messageResult, Exception? exception, ErgosfareContext context)
                {
                    Sink.Entries.Add("final:" + (exception == null ? "clean" : exception.Message));
                    return default;
                }
            }

            // --- exception swallow on a reference-typed result pipeline --------------

            public sealed class GenSwallowCommand : ICommand<string> { }

            public sealed class GenSwallowCommandHandler : ICommandHandler<GenSwallowCommand, string>
            {
                public ValueTask<string> HandleAsync(GenSwallowCommand command, ErgosfareContext context)
                {
                    Sink.Entries.Add("handler");
                    throw new InvalidOperationException("boom");
                }
            }

            public sealed class GenSwallowCommandExceptionInterceptor : ICommandExceptionInterceptor<GenSwallowCommand>
            {
                public ValueTask<object> HandleAsync(GenSwallowCommand command, object? messageResult, Exception exception, ErgosfareContext context)
                {
                    Sink.Entries.Add("exception:" + exception.Message);
                    return ValueTask.FromResult<object>(messageResult!);
                }
            }

            public sealed class GenSwallowCommandFinal : ICommandFinalInterceptor<GenSwallowCommand>
            {
                public ValueTask HandleAsync(GenSwallowCommand command, object? messageResult, Exception? exception, ErgosfareContext context)
                {
                    Sink.Entries.Add("final:" + (exception == null ? "clean" : exception.Message));
                    return default;
                }
            }

            // --- exception propagation (no exception stage) -----------------------

            public sealed class GenPropagateCommand : ICommand { }

            public sealed class GenPropagateCommandHandler : ICommandHandler<GenPropagateCommand>
            {
                public ValueTask HandleAsync(GenPropagateCommand command, ErgosfareContext context)
                {
                    throw new InvalidOperationException("unhandled");
                }
            }

            public sealed class GenPropagateCommandFinal : ICommandFinalInterceptor<GenPropagateCommand>
            {
                public ValueTask HandleAsync(GenPropagateCommand command, object? messageResult, Exception? exception, ErgosfareContext context)
                {
                    Sink.Entries.Add("final:" + (exception == null ? "clean" : exception.Message));
                    return default;
                }
            }

            // --- abort: exception stage skipped, finals run -----------------------

            public sealed class GenAbortCommand : ICommand<string> { }

            public sealed class GenAbortCommandHandler : ICommandHandler<GenAbortCommand, string>
            {
                public ValueTask<string> HandleAsync(GenAbortCommand command, ErgosfareContext context)
                {
                    Sink.Entries.Add("handler");
                    return ValueTask.FromResult("done");
                }
            }

            public sealed class GenAbortCommandPre : ICommandPreInterceptor<GenAbortCommand>
            {
                public ValueTask<GenAbortCommand> HandleAsync(GenAbortCommand command, ErgosfareContext context)
                {
                    Sink.Entries.Add("pre");
                    context.Abort();
                    return ValueTask.FromResult(command);
                }
            }

            public sealed class GenAbortCommandExceptionInterceptor : ICommandExceptionInterceptor<GenAbortCommand>
            {
                public ValueTask<object> HandleAsync(GenAbortCommand command, object? messageResult, Exception exception, ErgosfareContext context)
                {
                    Sink.Entries.Add("exception");
                    return ValueTask.FromResult<object>(messageResult!);
                }
            }

            public sealed class GenAbortCommandFinal : ICommandFinalInterceptor<GenAbortCommand>
            {
                public ValueTask HandleAsync(GenAbortCommand command, object? messageResult, Exception? exception, ErgosfareContext context)
                {
                    Sink.Entries.Add("final");
                    return default;
                }
            }

            // --- result rewrite ---------------------------------------------------

            public sealed class GenRewriteQuery : IQuery<int> { }

            public sealed class GenRewriteQueryHandler : IQueryHandler<GenRewriteQuery, int>
            {
                public ValueTask<int> HandleAsync(GenRewriteQuery query, ErgosfareContext context)
                    => ValueTask.FromResult(7);
            }

            public sealed class GenRewriteQueryPost : IQueryPostInterceptor<GenRewriteQuery, int>
            {
                public ValueTask<int> HandleAsync(GenRewriteQuery query, int queryResult, ErgosfareContext context)
                    => ValueTask.FromResult(queryResult + 35);
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
        var registerCommands = registrations.GetMethod("RegisterGenerated", [typeof(CommandModuleBuilder)])!;
        var registerQueries = registrations.GetMethod("RegisterGenerated", [typeof(QueryModuleBuilder)])!;

        var provider = new ServiceCollection()
            .AddErgosfare(options =>
            {
                options.AddCommandModule(commands => registerCommands.Invoke(null, [commands]));
                options.AddQueryModule(queries => registerQueries.Invoke(null, [queries]));
            })
            .BuildServiceProvider();

        return (assembly, provider);
    });

    private static List<string> Entries
        => (List<string>)Host.Value.Assembly.GetType("TestApp.Sink", throwOnError: true)!
            .GetField("Entries", BindingFlags.Public | BindingFlags.Static)!
            .GetValue(null)!;

    private static ICommand CreateCommand(string typeName)
        => (ICommand)Activator.CreateInstance(Host.Value.Assembly.GetType(typeName, throwOnError: true)!)!;

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task HappyPath_RewritesTheMessageAndRunsStagesInOrder()
    {
        var (assembly, provider) = Host.Value;

        Assert.NotNull(GeneratedDispatchRoots.FindStagedVoidPlan(assembly.GetType("TestApp.GenHappyCommand")!));

        Entries.Clear();
        await provider.GetRequiredService<ICommandMediator>().SendAsync(CreateCommand("TestApp.GenHappyCommand"));

        // The handler and post stage see the pre-interceptor's rewritten instance,
        // exactly like the strategy path; the final stage observes a clean outcome last.
        Assert.Equal(["pre:original", "handler:rewritten", "post:rewritten", "final:clean"], Entries);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task VoidHandlerException_RunsTheExceptionStageAndSwallows()
    {
        var (assembly, provider) = Host.Value;

        // The re-based (result-agnostic) flavored exception contract makes this pipeline
        // both runnable at all on the strategy path and modelable for a staged plan.
        Assert.NotNull(GeneratedDispatchRoots.FindStagedVoidPlan(assembly.GetType("TestApp.GenVoidSwallowCommand")!));

        Entries.Clear();

        await provider.GetRequiredService<ICommandMediator>().SendAsync(CreateCommand("TestApp.GenVoidSwallowCommand"));

        Assert.Equal(["handler", "exception:void-boom", "final:void-boom"], Entries);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task HandlerException_RunsTheExceptionStageAndSwallows()
    {
        var (assembly, provider) = Host.Value;

        var commandType = assembly.GetType("TestApp.GenSwallowCommand", throwOnError: true)!;
        Assert.NotNull(GeneratedDispatchRoots.FindStagedResultPlan(commandType, typeof(string)));

        Entries.Clear();

        // With an exception stage present the strategy swallows after the interceptors
        // observed the exception — the dispatch completes normally, returning the
        // (never produced) default result.
        var command = (ICommand<string>)Activator.CreateInstance(commandType)!;
        var result = await provider.GetRequiredService<ICommandMediator>().SendAsync(command);

        Assert.Null(result);
        Assert.Equal(["handler", "exception:boom", "final:boom"], Entries);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task HandlerException_WithoutExceptionStage_PropagatesAfterFinals()
    {
        var (assembly, provider) = Host.Value;

        Assert.NotNull(GeneratedDispatchRoots.FindStagedVoidPlan(assembly.GetType("TestApp.GenPropagateCommand")!));

        Entries.Clear();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await provider.GetRequiredService<ICommandMediator>().SendAsync(CreateCommand("TestApp.GenPropagateCommand")));

        Assert.Equal("unhandled", exception.Message);
        Assert.Equal(["final:unhandled"], Entries);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task Abort_CutsThePlanAndReachesTheCaller()
    {
        var (assembly, provider) = Host.Value;

        var commandType = assembly.GetType("TestApp.GenAbortCommand", throwOnError: true)!;
        Assert.NotNull(GeneratedDispatchRoots.FindStagedResultPlan(commandType, typeof(string)));

        Entries.Clear();

        var command = (ICommand<string>)Activator.CreateInstance(commandType)!;
        var mediator = provider.GetRequiredService<ICommandMediator>();

        // The emitted plan stops where the strategy would: the handler never runs, the
        // exception stage never sees the abort, the final stage does not run either, and
        // the signal reaches the caller — the strategy's exact contract, baked.
        await Assert.ThrowsAsync<ExecutionAbortedException>(async () => await mediator.SendAsync(command));

        Assert.Equal(["pre"], Entries);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public void FrozenComposition_MirrorsThePipelineTheDispatchActuallyRuns()
    {
        var (assembly, _) = Host.Value;
        var commandType = assembly.GetType("TestApp.GenHappyCommand", throwOnError: true)!;

        // The dual-run parity seam: the generator's frozen table derives, for the same
        // message, exactly the stage sequence the registry-backed dispatch executes —
        // which HappyPath_RewritesTheMessageAndRunsStagesInOrder pins observationally.
        var frozen = GeneratedDispatchRoots.FindFrozenComposition(commandType);
        Assert.NotNull(frozen);

        var shape = frozen!.BuildShape(commandType, []);

        Assert.Equal([assembly.GetType("TestApp.GenHappyCommandHandler")!], shape.Handlers);
        Assert.Empty(shape.IndirectHandlers);
        Assert.Equal([assembly.GetType("TestApp.GenHappyCommandPre")!], shape.PreInterceptors);
        Assert.Equal([assembly.GetType("TestApp.GenHappyCommandPost")!], shape.PostInterceptors);
        Assert.Empty(shape.ExceptionInterceptors);
        Assert.Equal([assembly.GetType("TestApp.GenHappyCommandFinal")!], shape.FinalInterceptors);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task PostInterceptor_RewritesTheResult()
    {
        var (assembly, _) = Host.Value;

        var queryType = assembly.GetType("TestApp.GenRewriteQuery", throwOnError: true)!;
        Assert.NotNull(GeneratedDispatchRoots.FindStagedResultPlan(queryType, typeof(int)));

        var query = (IQuery<int>)Activator.CreateInstance(queryType)!;
        var result = await Host.Value.Provider.GetRequiredService<IQueryMediator>().QueryAsync(query);

        Assert.Equal(42, result);
    }
}
