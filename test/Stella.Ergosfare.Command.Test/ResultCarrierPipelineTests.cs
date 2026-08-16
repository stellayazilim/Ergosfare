using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Abstractions.Results;

namespace Stella.Ergosfare.Command.Test;

/// <summary>
/// End-to-end value channel: a handler that returns a failed <see cref="Result{TValue}"/>
/// bypasses throw entirely, yet the pipeline still routes the carried exception to the
/// exception-interceptor stage through the carrier's built-in adapter.
/// </summary>
public class ResultCarrierPipelineTests
{
    public sealed record CarrierCommand(bool Fail) : ICommand<Result<string>>;

    public sealed class CarrierHandler : ICommandHandler<CarrierCommand, Result<string>>
    {
        public ValueTask<Result<string>> HandleAsync(CarrierCommand message, ErgosfareContext context)
            => new(message.Fail
                ? Result<string>.Fail(new InvalidOperationException("carried"))
                : Result<string>.Ok("done"));
    }

    public sealed class CarrierExceptionObserver
        : ICommandExceptionInterceptorFor<CarrierCommand, Result<string>, InvalidOperationException>
    {
        public static Exception? Observed;

        public ValueTask<Result<string>> HandleAsync(CarrierCommand message, Result<string> result, InvalidOperationException exception, ErgosfareContext context)
        {
            Observed = exception;
            return new ValueTask<Result<string>>(Result<string>.Fail(exception));
        }
    }

    [Fact]
    public async Task A_carried_failure_reaches_the_exception_stage_without_a_throw_from_the_handler()
    {
        CarrierExceptionObserver.Observed = null;

        var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c => c
                .Register<CarrierHandler>()
                .Register<CarrierExceptionObserver>()))
            .BuildServiceProvider();

        var mediator = provider.GetRequiredService<ICommandMediator>();

        var ok = await mediator.SendAsync(new CarrierCommand(Fail: false));
        Assert.True(ok.IsSuccess);
        Assert.Null(CarrierExceptionObserver.Observed);

        var failed = await mediator.SendAsync(new CarrierCommand(Fail: true));
        Assert.NotNull(CarrierExceptionObserver.Observed);
        Assert.Equal("carried", CarrierExceptionObserver.Observed.Message);
        Assert.False(failed.IsSuccess);
    }

    [Fact]
    public async Task A_carried_failure_with_nobody_to_tell_flows_out_as_the_failed_carrier()
    {
        var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c => c.Register<CarrierHandler>()))
            .BuildServiceProvider();

        // Choosing a materializable carrier is choosing throwlessness: with no exception
        // stage at all, the failed carrier is the result — nothing is thrown.
        var failed = await provider.GetRequiredService<ICommandMediator>().SendAsync(new CarrierCommand(Fail: true));

        Assert.False(failed.IsSuccess);
        Assert.Equal("carried", failed.Exception!.Message);
    }

    public sealed record ThrowingCarrierCommand : ICommand<Result<string>>;

    public sealed class ThrowingCarrierHandler : ICommandHandler<ThrowingCarrierCommand, Result<string>>
    {
        public ValueTask<Result<string>> HandleAsync(ThrowingCarrierCommand message, ErgosfareContext context)
            => throw new InvalidOperationException("thrown");
    }

    public sealed class ThrowingCarrierFinalObserver : ICommandFinalInterceptor<ThrowingCarrierCommand>
    {
        public static Exception? Observed;

        public ValueTask HandleAsync(ThrowingCarrierCommand message, object? messageResult, Exception? exception, ErgosfareContext context)
        {
            Observed = exception;
            return default;
        }
    }

    [Fact]
    public async Task A_real_throw_in_a_carrier_pipeline_materializes_into_a_failed_carrier()
    {
        ThrowingCarrierFinalObserver.Observed = null;

        var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c => c
                .Register<ThrowingCarrierHandler>()
                .Register<ThrowingCarrierFinalObserver>()))
            .BuildServiceProvider();

        // Catch-materialization: the throw never reaches the caller; the failure comes
        // back inside the carrier, and the final stage still observes the exception.
        var failed = await provider.GetRequiredService<ICommandMediator>().SendAsync(new ThrowingCarrierCommand());

        Assert.False(failed.IsSuccess);
        Assert.Equal("thrown", failed.Exception!.Message);
        Assert.NotNull(ThrowingCarrierFinalObserver.Observed);
        Assert.Equal("thrown", ThrowingCarrierFinalObserver.Observed.Message);
    }

    public sealed record FilteredCarrierCommand : ICommand<Result<string>>;

    public sealed class FilteredCarrierHandler : ICommandHandler<FilteredCarrierCommand, Result<string>>
    {
        public ValueTask<Result<string>> HandleAsync(FilteredCarrierCommand message, ErgosfareContext context)
            => throw new InvalidOperationException("thrown");
    }

    public sealed class FilteredCarrierObserver
        : ICommandExceptionInterceptorFor<FilteredCarrierCommand, Result<string>, ArgumentException>
    {
        public static bool Ran;

        public ValueTask<Result<string>> HandleAsync(FilteredCarrierCommand message, Result<string> result, ArgumentException exception, ErgosfareContext context)
        {
            Ran = true;
            return new ValueTask<Result<string>>(result);
        }
    }

    [Fact]
    public async Task An_unmatched_exception_stage_leaves_the_materialized_failure_standing()
    {
        FilteredCarrierObserver.Ran = false;

        var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c => c
                .Register<FilteredCarrierHandler>()
                .Register<FilteredCarrierObserver>()))
            .BuildServiceProvider();

        // The interceptor accepts only ArgumentException, so nobody handles the throw —
        // but the carrier absorbed it, so no rethrow happens either.
        var failed = await provider.GetRequiredService<ICommandMediator>().SendAsync(new FilteredCarrierCommand());

        Assert.False(FilteredCarrierObserver.Ran);
        Assert.False(failed.IsSuccess);
        Assert.Equal("thrown", failed.Exception!.Message);
    }

    public sealed record PostCarrierCommand : ICommand<Result<string>>;

    public sealed class PostCarrierHandler : ICommandHandler<PostCarrierCommand, Result<string>>
    {
        public ValueTask<Result<string>> HandleAsync(PostCarrierCommand message, ErgosfareContext context)
            => new(Result<string>.Ok("done"));
    }

    public sealed class PostCarrierFailingPost : ICommandPostInterceptor<PostCarrierCommand, Result<string>>
    {
        public ValueTask<Result<string>> HandleAsync(PostCarrierCommand message, Result<string> messageResult, ErgosfareContext context)
            => new(Result<string>.Fail(new InvalidOperationException("post-carried")));
    }

    public sealed class PostCarrierRecordingPost : ICommandPostInterceptor<PostCarrierCommand, Result<string>>
    {
        public static bool Ran;

        public ValueTask<Result<string>> HandleAsync(PostCarrierCommand message, Result<string> messageResult, ErgosfareContext context)
        {
            Ran = true;
            return new(messageResult);
        }
    }

    [Fact]
    public async Task A_failed_carrier_from_a_post_interceptor_skips_the_remaining_posts_and_becomes_the_result()
    {
        PostCarrierRecordingPost.Ran = false;

        var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c => c
                .Register<PostCarrierHandler>()
                .Register<PostCarrierFailingPost>()
                .Register<PostCarrierRecordingPost>()))
            .BuildServiceProvider();

        var failed = await provider.GetRequiredService<ICommandMediator>().SendAsync(new PostCarrierCommand());

        // Exactly like a thrown failure, the carried one stops the post stage where it
        // appeared; the failed carrier the post produced is the pipeline's result.
        Assert.False(PostCarrierRecordingPost.Ran);
        Assert.False(failed.IsSuccess);
        Assert.Equal("post-carried", failed.Exception!.Message);
    }

    public sealed class ForeignOutcome
    {
        public Exception? Error { get; init; }
        public string? Value { get; init; }
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
    public sealed record ForeignCarrierCommand : ICommand<ForeignOutcome>;

    public sealed class ForeignCarrierHandler : ICommandHandler<ForeignCarrierCommand, ForeignOutcome>
    {
        public ValueTask<ForeignOutcome> HandleAsync(ForeignCarrierCommand message, ErgosfareContext context)
            => new(new ForeignOutcome { Error = new InvalidOperationException("foreign-carried") });
    }

    public sealed class ForeignCarrierFinalObserver : ICommandFinalInterceptor<ForeignCarrierCommand>
    {
        public static Exception? Observed;

        public ValueTask HandleAsync(ForeignCarrierCommand message, object? messageResult, Exception? exception, ErgosfareContext context)
        {
            Observed = exception;
            return default;
        }
    }

    [Fact]
    public async Task A_carried_failure_in_a_non_materializable_carrier_surfaces_as_a_throw_after_finals()
    {
        ForeignCarrierFinalObserver.Observed = null;

        var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c => c
                .Register<ForeignCarrierHandler>()
                .Register<ForeignCarrierFinalObserver>()))
            .BuildServiceProvider();

        // The foreign adapter can extract but not absorb: with nobody handling the
        // carried failure, the classic contract stands — it surfaces as a throw, and the
        // final stage has already observed it.
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await provider.GetRequiredService<ICommandMediator>().SendAsync(new ForeignCarrierCommand()));

        Assert.Equal("foreign-carried", thrown.Message);
        Assert.NotNull(ForeignCarrierFinalObserver.Observed);
        Assert.Equal("foreign-carried", ForeignCarrierFinalObserver.Observed.Message);
    }

    public sealed class DefaultBoundOutcome
    {
        public Exception? Error { get; init; }
        public string? Value { get; init; }
    }

    public sealed class DefaultBoundOutcomeAdapter : IResultAdapter<DefaultBoundOutcome>
    {
        public bool TryGetException(in DefaultBoundOutcome result, out Exception? exception)
        {
            exception = result.Error;
            return exception is not null;
        }
    }

    public sealed record DefaultBoundCommand : ICommand<DefaultBoundOutcome>;

    public sealed class DefaultBoundHandler : ICommandHandler<DefaultBoundCommand, DefaultBoundOutcome>
    {
        public ValueTask<DefaultBoundOutcome> HandleAsync(DefaultBoundCommand message, ErgosfareContext context)
            => new(new DefaultBoundOutcome { Error = new InvalidOperationException("default-carried") });
    }

    public sealed class DefaultBoundObserver : ICommandExceptionInterceptor<DefaultBoundCommand>
    {
        public static Exception? Observed;

        public ValueTask<object> HandleAsync(DefaultBoundCommand message, object? messageResult, Exception exception, ErgosfareContext context)
        {
            Observed = exception;
            return ValueTask.FromResult<object>(new DefaultBoundOutcome { Value = "recovered" });
        }
    }

    [Fact]
    public async Task An_unannotated_message_falls_back_to_the_default_adapter_configured_in_AddErgosfare()
    {
        DefaultBoundObserver.Observed = null;

        var provider = new ServiceCollection()
            .AddErgosfare(x => x
                .UseDefaultResultAdapter(typeof(DefaultBoundOutcomeAdapter))
                .AddCommandModule(c => c
                    .Register<DefaultBoundHandler>()
                    .Register<DefaultBoundObserver>()))
            .BuildServiceProvider();

        // No annotation anywhere: the carried failure still reaches the exception stage
        // through the application-wide default, and the interceptor's replacement wins.
        var result = await provider.GetRequiredService<ICommandMediator>().SendAsync(new DefaultBoundCommand());

        Assert.NotNull(DefaultBoundObserver.Observed);
        Assert.Equal("default-carried", DefaultBoundObserver.Observed.Message);
        Assert.Equal("recovered", result.Value);
    }

    [Fact]
    public async Task Without_a_default_adapter_the_same_pipeline_keeps_the_classic_semantics()
    {
        DefaultBoundObserver.Observed = null;

        var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c => c
                .Register<DefaultBoundHandler>()
                .Register<DefaultBoundObserver>()))
            .BuildServiceProvider();

        // Nobody is forced onto the value channel: with no default configured and no
        // annotation, nothing probes — the failed carrier is just a return value and the
        // exception stage never runs.
        var result = await provider.GetRequiredService<ICommandMediator>().SendAsync(new DefaultBoundCommand());

        Assert.Null(DefaultBoundObserver.Observed);
        Assert.Equal("default-carried", result.Error!.Message);
    }

    [IgnoreResultAdapter]
    public sealed record OptedOutCommand : ICommand<DefaultBoundOutcome>;

    public sealed class OptedOutHandler : ICommandHandler<OptedOutCommand, DefaultBoundOutcome>
    {
        public ValueTask<DefaultBoundOutcome> HandleAsync(OptedOutCommand message, ErgosfareContext context)
            => new(new DefaultBoundOutcome { Error = new InvalidOperationException("opted-out") });
    }

    public sealed class OptedOutObserver : ICommandExceptionInterceptor<OptedOutCommand>
    {
        public static bool Ran;

        public ValueTask<object> HandleAsync(OptedOutCommand message, object? messageResult, Exception exception, ErgosfareContext context)
        {
            Ran = true;
            return ValueTask.FromResult(messageResult!);
        }
    }

    [Fact]
    public async Task IgnoreResultAdapter_OptsTheMessageOutOfTheConfiguredDefault()
    {
        OptedOutObserver.Ran = false;

        var provider = new ServiceCollection()
            .AddErgosfare(x => x
                .UseDefaultResultAdapter(typeof(DefaultBoundOutcomeAdapter))
                .AddCommandModule(c => c
                    .Register<OptedOutHandler>()
                    .Register<OptedOutObserver>()))
            .BuildServiceProvider();

        // The escape hatch: the default is configured, yet this message keeps the
        // classic try/catch semantics — the carried failure is just a return value.
        var result = await provider.GetRequiredService<ICommandMediator>().SendAsync(new OptedOutCommand());

        Assert.False(OptedOutObserver.Ran);
        Assert.Equal("opted-out", result.Error!.Message);
    }

    [IgnoreResultAdapter]
    public sealed record OptedOutNativeCommand : ICommand<Result<string>>;

    public sealed class OptedOutNativeHandler : ICommandHandler<OptedOutNativeCommand, Result<string>>
    {
        public ValueTask<Result<string>> HandleAsync(OptedOutNativeCommand message, ErgosfareContext context)
            => throw new InvalidOperationException("native-opt-out");
    }

    public sealed class OptedOutNativeFinal : ICommandFinalInterceptor<OptedOutNativeCommand>
    {
        public ValueTask HandleAsync(OptedOutNativeCommand message, object? messageResult, Exception? exception, ErgosfareContext context)
            => default;
    }

    [Fact]
    public async Task IgnoreResultAdapter_RestoresTheClassicThrowEvenForTheNativeCarrier()
    {
        var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c => c
                .Register<OptedOutNativeHandler>()
                .Register<OptedOutNativeFinal>()))
            .BuildServiceProvider();

        // Opting out suppresses the native tier too: no catch-materialization, the real
        // throw reaches the caller exactly as any unadapted pipeline's would.
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await provider.GetRequiredService<ICommandMediator>().SendAsync(new OptedOutNativeCommand()));

        Assert.Equal("native-opt-out", thrown.Message);
    }

    [IgnoreResultAdapter]
    public sealed record OptedOutThrowingCommand(bool Throw) : ICommand<Result<string>>;

    public sealed class OptedOutThrowingHandler : ICommandHandler<OptedOutThrowingCommand, Result<string>>
    {
        public ValueTask<Result<string>> HandleAsync(OptedOutThrowingCommand message, ErgosfareContext context)
            => message.Throw
                ? throw new InvalidOperationException("classic-thrown")
                : new ValueTask<Result<string>>(Result<string>.Fail(new InvalidOperationException("carried-unseen")));
    }

    public sealed class OptedOutThrowingObserver
        : ICommandExceptionInterceptorFor<OptedOutThrowingCommand, Result<string>, InvalidOperationException>
    {
        public static Exception? Observed;

        public ValueTask<Result<string>> HandleAsync(OptedOutThrowingCommand message, Result<string> result, InvalidOperationException exception, ErgosfareContext context)
        {
            Observed = exception;
            return new ValueTask<Result<string>>(Result<string>.Ok("handled"));
        }
    }

    [Fact]
    public async Task IgnoreResultAdapter_KeepsTheClassicLane_ThrowsReachTheExceptionStage_CarriersAreNeverProbed()
    {
        OptedOutThrowingObserver.Observed = null;

        var provider = new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c => c
                .Register<OptedOutThrowingHandler>()
                .Register<OptedOutThrowingObserver>()))
            .BuildServiceProvider();

        var mediator = provider.GetRequiredService<ICommandMediator>();

        // The classic lane, half one: a real throw is caught by the pipeline's try/catch
        // and the exception interceptors run — swallowing it, exactly as always.
        var handled = await mediator.SendAsync(new OptedOutThrowingCommand(Throw: true));
        Assert.NotNull(OptedOutThrowingObserver.Observed);
        Assert.Equal("classic-thrown", OptedOutThrowingObserver.Observed.Message);
        Assert.Equal("handled", handled.Value);

        // The classic lane, half two: a failure carried inside the result is never
        // looked for — the carrier is just a return value and the stage stays silent.
        OptedOutThrowingObserver.Observed = null;
        var carried = await mediator.SendAsync(new OptedOutThrowingCommand(Throw: false));
        Assert.Null(OptedOutThrowingObserver.Observed);
        Assert.Equal("carried-unseen", carried.Exception!.Message);
    }
}
