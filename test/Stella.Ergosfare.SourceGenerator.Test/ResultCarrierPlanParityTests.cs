using System.Reflection;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Abstractions.DispatchRoots;
using Stella.Ergosfare.Core.Abstractions.Results;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.SourceGenerator.Test;

/// <summary>
/// Strategy-parity matrix for the baked value channel, executed end to end like
/// <see cref="StagedPlanExecutionParityTests"/>: the emitted staged plans carry the probe
/// branches and materializing catches, the hosting executor's adapter-identity gate admits
/// them, and dispatches through the public mediator behave exactly as the runtime
/// strategy's two-channel semantics — a carried failure enters the exception stage
/// without a throw, a real throw materializes into a failed carrier, a non-materializable
/// carrier's unhandled failure surfaces after the final stage.
/// </summary>
public class ResultCarrierPlanParityTests
{
    private const string Source = """
        using System;
        using System.Collections.Generic;
        using System.Threading.Tasks;
        using Stella.Ergosfare.Commands.Abstractions;
        using Stella.Ergosfare.Core.Abstractions;
        using Stella.Ergosfare.Core.Abstractions.Results;
        using Stella.Ergosfare.Core.Abstractions.Attributes;

        namespace TestApp
        {
            public static class Sink
            {
                public static readonly List<string> Entries = new List<string>();
            }

            // --- carried failure reaches the exception stage without a throw ---------

            public sealed class GenCarrierSwallowCommand : ICommand<Result<string>> { }

            public sealed class GenCarrierSwallowCommandHandler : ICommandHandler<GenCarrierSwallowCommand, Result<string>>
            {
                public ValueTask<Result<string>> HandleAsync(GenCarrierSwallowCommand command, ErgosfareContext context)
                {
                    Sink.Entries.Add("handler");
                    return new ValueTask<Result<string>>(Result<string>.Fail(new InvalidOperationException("carried")));
                }
            }

            public sealed class GenCarrierSwallowCommandExceptionInterceptor : ICommandExceptionInterceptor<GenCarrierSwallowCommand>
            {
                public ValueTask<object> HandleAsync(GenCarrierSwallowCommand command, object? messageResult, Exception exception, ErgosfareContext context)
                {
                    Sink.Entries.Add("exception:" + exception.Message);
                    return ValueTask.FromResult<object>(messageResult!);
                }
            }

            public sealed class GenCarrierSwallowCommandFinal : ICommandFinalInterceptor<GenCarrierSwallowCommand>
            {
                public ValueTask HandleAsync(GenCarrierSwallowCommand command, object? messageResult, Exception? exception, ErgosfareContext context)
                {
                    Sink.Entries.Add("final:" + (exception == null ? "clean" : exception.Message));
                    return default;
                }
            }

            // --- real throw materializes into a failed carrier -----------------------

            public sealed class GenCarrierThrowCommand : ICommand<Result<string>> { }

            public sealed class GenCarrierThrowCommandHandler : ICommandHandler<GenCarrierThrowCommand, Result<string>>
            {
                public ValueTask<Result<string>> HandleAsync(GenCarrierThrowCommand command, ErgosfareContext context)
                {
                    Sink.Entries.Add("handler");
                    throw new InvalidOperationException("thrown");
                }
            }

            public sealed class GenCarrierThrowCommandFinal : ICommandFinalInterceptor<GenCarrierThrowCommand>
            {
                public ValueTask HandleAsync(GenCarrierThrowCommand command, object? messageResult, Exception? exception, ErgosfareContext context)
                {
                    Sink.Entries.Add("final:" + (exception == null ? "clean" : exception.Message));
                    return default;
                }
            }

            // --- a post's failed carrier stops the stage and becomes the result ------

            public sealed class GenPostCarrierCommand : ICommand<Result<string>> { }

            public sealed class GenPostCarrierCommandHandler : ICommandHandler<GenPostCarrierCommand, Result<string>>
            {
                public ValueTask<Result<string>> HandleAsync(GenPostCarrierCommand command, ErgosfareContext context)
                {
                    Sink.Entries.Add("handler");
                    return new ValueTask<Result<string>>(Result<string>.Ok("done"));
                }
            }

            public sealed class GenPostCarrierCommandPostA : ICommandPostInterceptor<GenPostCarrierCommand, Result<string>>
            {
                public ValueTask<Result<string>> HandleAsync(GenPostCarrierCommand command, Result<string> messageResult, ErgosfareContext context)
                {
                    Sink.Entries.Add("post-a");
                    return new ValueTask<Result<string>>(Result<string>.Fail(new InvalidOperationException("post-carried")));
                }
            }

            public sealed class GenPostCarrierCommandPostB : ICommandPostInterceptor<GenPostCarrierCommand, Result<string>>
            {
                public ValueTask<Result<string>> HandleAsync(GenPostCarrierCommand command, Result<string> messageResult, ErgosfareContext context)
                {
                    Sink.Entries.Add("post-b");
                    return new ValueTask<Result<string>>(messageResult);
                }
            }

            public sealed class GenPostCarrierCommandFinal : ICommandFinalInterceptor<GenPostCarrierCommand>
            {
                public ValueTask HandleAsync(GenPostCarrierCommand command, object? messageResult, Exception? exception, ErgosfareContext context)
                {
                    Sink.Entries.Add("final:" + (exception == null ? "clean" : exception.Message));
                    return default;
                }
            }

            // --- non-materializable foreign carrier: unhandled surfaces after finals -

            public sealed class ForeignOutcome
            {
                public Exception? Error { get; set; }
                public string? Value { get; set; }
            }

            public sealed class ForeignOutcomeAdapter : IResultAdapter<ForeignOutcome>
            {
                public bool TryGetException(in ForeignOutcome result, out Exception? exception)
                {
                    exception = result.Error;
                    return exception is not null;
                }
            }

            [ResultAdapter(typeof(ForeignOutcomeAdapter))]
            public sealed class GenForeignCommand : ICommand<ForeignOutcome> { }

            public sealed class GenForeignCommandHandler : ICommandHandler<GenForeignCommand, ForeignOutcome>
            {
                public ValueTask<ForeignOutcome> HandleAsync(GenForeignCommand command, ErgosfareContext context)
                {
                    Sink.Entries.Add("handler");
                    return new ValueTask<ForeignOutcome>(new ForeignOutcome { Error = new InvalidOperationException("foreign-carried") });
                }
            }

            public sealed class GenForeignCommandFinal : ICommandFinalInterceptor<GenForeignCommand>
            {
                public ValueTask HandleAsync(GenForeignCommand command, object? messageResult, Exception? exception, ErgosfareContext context)
                {
                    Sink.Entries.Add("final:" + (exception == null ? "clean" : exception.Message));
                    return default;
                }
            }

            // --- materializing foreign carrier absorbs a real throw ------------------

            public sealed class AbsorbentOutcome
            {
                public Exception? Error { get; set; }
            }

            public sealed class AbsorbentOutcomeAdapter : IResultAdapter<AbsorbentOutcome>, IResultMaterializer<AbsorbentOutcome>
            {
                public bool TryGetException(in AbsorbentOutcome result, out Exception? exception)
                {
                    exception = result.Error;
                    return exception is not null;
                }

                public AbsorbentOutcome Materialize(Exception exception)
                    => new AbsorbentOutcome { Error = exception };
            }

            [ResultAdapter(typeof(AbsorbentOutcomeAdapter))]
            public sealed class GenAbsorbentCommand : ICommand<AbsorbentOutcome> { }

            public sealed class GenAbsorbentCommandHandler : ICommandHandler<GenAbsorbentCommand, AbsorbentOutcome>
            {
                public ValueTask<AbsorbentOutcome> HandleAsync(GenAbsorbentCommand command, ErgosfareContext context)
                {
                    Sink.Entries.Add("handler");
                    throw new InvalidOperationException("absorbed");
                }
            }

            public sealed class GenAbsorbentCommandFinal : ICommandFinalInterceptor<GenAbsorbentCommand>
            {
                public ValueTask HandleAsync(GenAbsorbentCommand command, object? messageResult, Exception? exception, ErgosfareContext context)
                {
                    Sink.Entries.Add("final:" + (exception == null ? "clean" : exception.Message));
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
        var registerCommands = registrations.GetMethod("RegisterGenerated", [typeof(CommandModuleBuilder)])!;

        var provider = new ServiceCollection()
            .AddErgosfare(options => options.AddCommandModule(commands => registerCommands.Invoke(null, [commands])))
            .BuildServiceProvider();

        return (assembly, provider);
    });

    private static List<string> Entries
        => (List<string>)Host.Value.Assembly.GetType("TestApp.Sink", throwOnError: true)!
            .GetField("Entries", BindingFlags.Public | BindingFlags.Static)!
            .GetValue(null)!;

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task CarriedFailure_RunsTheExceptionStageWithoutAThrow()
    {
        var (assembly, provider) = Host.Value;

        var commandType = assembly.GetType("TestApp.GenCarrierSwallowCommand", throwOnError: true)!;
        var plan = GeneratedDispatchRoots.FindStagedResultPlan(commandType, typeof(Result<string>));
        Assert.NotNull(plan);

        // The baked adapter identity is the executor gate's admission ticket.
        Assert.Equal(typeof(ResultExceptionAdapter<string>), plan.Composition.ResultAdapterType);

        Entries.Clear();

        var command = (ICommand<Result<string>>)Activator.CreateInstance(commandType)!;
        var result = await provider.GetRequiredService<ICommandMediator>().SendAsync(command);

        Assert.False(result.IsSuccess);
        Assert.Equal("carried", result.Exception!.Message);
        Assert.Equal(["handler", "exception:carried", "final:carried"], Entries);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task RealThrow_MaterializesIntoAFailedCarrier()
    {
        var (assembly, provider) = Host.Value;

        var commandType = assembly.GetType("TestApp.GenCarrierThrowCommand", throwOnError: true)!;
        Assert.NotNull(GeneratedDispatchRoots.FindStagedResultPlan(commandType, typeof(Result<string>)));

        Entries.Clear();

        var command = (ICommand<Result<string>>)Activator.CreateInstance(commandType)!;
        var result = await provider.GetRequiredService<ICommandMediator>().SendAsync(command);

        // No exception stage exists, yet nothing reaches the caller as a throw: the
        // carrier absorbed the failure, and the final stage still observed it.
        Assert.False(result.IsSuccess);
        Assert.Equal("thrown", result.Exception!.Message);
        Assert.Equal(["handler", "final:thrown"], Entries);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task PostCarriedFailure_StopsTheStageAndBecomesTheResult()
    {
        var (assembly, provider) = Host.Value;

        var commandType = assembly.GetType("TestApp.GenPostCarrierCommand", throwOnError: true)!;
        Assert.NotNull(GeneratedDispatchRoots.FindStagedResultPlan(commandType, typeof(Result<string>)));

        Entries.Clear();

        var command = (ICommand<Result<string>>)Activator.CreateInstance(commandType)!;
        var result = await provider.GetRequiredService<ICommandMediator>().SendAsync(command);

        // Post B never runs — exactly as a thrown failure would skip it — and the failed
        // carrier post A produced is the pipeline's result.
        Assert.False(result.IsSuccess);
        Assert.Equal("post-carried", result.Exception!.Message);
        Assert.Equal(["handler", "post-a", "final:post-carried"], Entries);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ForeignCarriedFailure_SurfacesAsAThrowAfterFinals()
    {
        var (assembly, provider) = Host.Value;

        var commandType = assembly.GetType("TestApp.GenForeignCommand", throwOnError: true)!;
        var plan = GeneratedDispatchRoots.FindStagedResultPlan(commandType, assembly.GetType("TestApp.ForeignOutcome", throwOnError: true)!);
        Assert.NotNull(plan);
        Assert.Equal(assembly.GetType("TestApp.ForeignOutcomeAdapter"), plan.Composition.ResultAdapterType);

        Entries.Clear();

        var command = Activator.CreateInstance(commandType)!;
        var mediator = provider.GetRequiredService<ICommandMediator>();

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await SendReflective(mediator, command, assembly.GetType("TestApp.ForeignOutcome", throwOnError: true)!));

        Assert.Equal("foreign-carried", thrown.Message);
        Assert.Equal(["handler", "final:foreign-carried"], Entries);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task MaterializingForeignCarrier_AbsorbsARealThrow()
    {
        var (assembly, provider) = Host.Value;

        var commandType = assembly.GetType("TestApp.GenAbsorbentCommand", throwOnError: true)!;
        var outcomeType = assembly.GetType("TestApp.AbsorbentOutcome", throwOnError: true)!;
        Assert.NotNull(GeneratedDispatchRoots.FindStagedResultPlan(commandType, outcomeType));

        Entries.Clear();

        var command = Activator.CreateInstance(commandType)!;
        var outcome = await SendReflective(provider.GetRequiredService<ICommandMediator>(), command, outcomeType);

        var error = (Exception?)outcomeType.GetProperty("Error")!.GetValue(outcome);
        Assert.NotNull(error);
        Assert.Equal("absorbed", error.Message);
        Assert.Equal(["handler", "final:absorbed"], Entries);
    }

    /// <summary>
    /// Dispatches an <c>ICommand&lt;TResult&gt;</c> whose result type lives in the emitted
    /// assembly, closing the full <c>SendAsync</c> overload reflectively and unwrapping the
    /// boxed <c>ValueTask&lt;TResult&gt;</c>.
    /// </summary>
    private static async Task<object?> SendReflective(ICommandMediator mediator, object command, Type resultType)
    {
        var send = typeof(ICommandMediator).GetMethods()
            .Single(m => m.Name == "SendAsync" && m.IsGenericMethodDefinition
                && m.GetParameters() is { Length: 3 } parameters
                && parameters[1].ParameterType == typeof(IEnumerable<string>))
            .MakeGenericMethod(resultType);

        var valueTask = send.Invoke(mediator, [command, null, default(CancellationToken)])!;
        var task = (Task)valueTask.GetType().GetMethod("AsTask")!.Invoke(valueTask, null)!;
        await task;
        return task.GetType().GetProperty("Result")!.GetValue(task);
    }
}
