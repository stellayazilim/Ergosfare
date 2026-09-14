#pragma warning disable CS0618, ERGOEXP003

using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Exceptions;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Abstractions.Streaming;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Queries.Abstractions;
using Stella.Ergosfare.Queries.Abstractions.Streaming;
using Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection;

namespace Stella.Ergosfare.Queries.Test;

public sealed class DuplexProbe() : QueryStream<string, DuplexProbe>(capacity: 1), IStreamQuery<string>
{
    public bool FailAfterFirst { get; init; }
    public bool Reject { get; init; }
    public bool FailFinal { get; init; }
    public int FinalCalls { get; set; }
    public int ExceptionCalls { get; set; }
    public Exception? FinalException { get; set; }
    public TaskCompletionSource HandlerDisposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}

public sealed class DuplexProbeHandler : IStreamQueryHandler<DuplexProbe, string>
{
    public async IAsyncEnumerable<string> StreamAsync(DuplexProbe query, ErgosfareContext context)
    {
        var text = "";
        try
        {
            await foreach (var chunk in query.WithCancellation(context.CancellationToken))
            {
                text += chunk;
                yield return text;
                if (query.FailAfterFirst)
                    throw new InvalidOperationException("handler failed");
            }
        }
        finally
        {
            query.HandlerDisposed.TrySetResult();
        }
    }
}

public sealed class DuplexProbePreInterceptor : IQueryPreInterceptor<DuplexProbe>
{
    public ValueTask<DuplexProbe> HandleAsync(DuplexProbe query, ErgosfareContext context)
        => query.Reject
            ? throw new InvalidOperationException("dispatch refused")
            : ValueTask.FromResult(query);
}

public sealed class DuplexProbeFinal : IQuery, IAsyncFinalInterceptor<DuplexProbe>
{
    public ValueTask HandleAsync(DuplexProbe message, object? result, Exception? exception, ErgosfareContext context)
    {
        message.FinalCalls++;
        message.FinalException = exception;
        if (message.FailFinal) throw new ArgumentException("final failed");
        return default;
    }
}

public sealed class DuplexProbeException : IQuery, IAsyncExceptionInterceptor<DuplexProbe>
{
    public ValueTask<object> HandleAsync(DuplexProbe message, object? result, Exception exception, ErgosfareContext context)
    {
        message.ExceptionCalls++;
        return ValueTask.FromResult(result!);
    }
}

[Trait("Category", "Unit")]
public class DuplexStreamLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BoundSource_IsCancelledAndDisposedBeforeDispatchReturns(bool reject)
    {
        await using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var disposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async IAsyncEnumerable<string> Source(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            try
            {
                yield return "a";
                await Task.Delay(Timeout.Infinite, ct);
            }
            finally { disposed.TrySetResult(); }
        }
        await using var message = new DuplexProbe { Reject = reject }.Pipe(Source(), timeout.Token);
        await using var output = scope.ServiceProvider.GetRequiredService<IQueryMediator>()
            .StreamAsync(message, timeout.Token).GetAsyncEnumerator();
        if (reject)
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await output.MoveNextAsync());
        else
        {
            Assert.True(await output.MoveNextAsync());
            Assert.Equal("a", output.Current);
            await output.DisposeAsync();
        }
        Assert.True(disposed.Task.IsCompleted);
        Assert.Equal(1, message.FinalCalls);
    }

    private static ServiceProvider BuildProvider() => new ServiceCollection()
        .AddErgosfare(x => x.AddQueryModule(q => q
            .Register<DuplexProbeHandler>().Register<DuplexProbePreInterceptor>()
            .Register<DuplexProbeFinal>().Register<DuplexProbeException>()))
        .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

    [Fact]
    public async Task OutputArrivesWhileInputIsOpen_AndCompletionDrainsBufferedInput()
    {
        await using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IQueryMediator>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var message = new DuplexProbe();
        await using var output = mediator.StreamAsync(message, timeout.Token).GetAsyncEnumerator();

        var first = output.MoveNextAsync();
        Assert.False(first.IsCompleted);
        await message.WriteAsync("a", timeout.Token);
        Assert.True(await first);
        Assert.Equal("a", output.Current);
        Assert.Equal(StreamCompletion.Open, message.Info.Completion);

        await message.WriteAsync("b", timeout.Token);
        var blockedWrite = message.WriteAsync("c", timeout.Token);
        Assert.False(blockedWrite.IsCompleted);
        Assert.True(await output.MoveNextAsync());
        Assert.Equal("ab", output.Current);
        await blockedWrite;
        message.Complete();
        Assert.True(await output.MoveNextAsync());
        Assert.Equal("abc", output.Current);
        Assert.False(await output.MoveNextAsync());
        Assert.True(message.HandlerDisposed.Task.IsCompleted);
        Assert.Equal(3, message.Info.Chunks);
        Assert.Equal(1, message.FinalCalls);
        Assert.Null(message.FinalException);
    }

    [Fact]
    public async Task ProducerFailure_ReachesOutputConsumer_AndDisposesHandler()
    {
        await using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var message = new DuplexProbe();
        await using var output = scope.ServiceProvider.GetRequiredService<IQueryMediator>()
            .StreamAsync(message, timeout.Token).GetAsyncEnumerator();
        var pendingRead = output.MoveNextAsync();
        var error = new InvalidOperationException("producer failed");
        message.Fault(error);
        Assert.Same(error, await Assert.ThrowsAsync<InvalidOperationException>(async () => await pendingRead));
        Assert.True(message.HandlerDisposed.Task.IsCompleted);
        Assert.Equal(StreamCompletion.Faulted, message.Info.Completion);
        Assert.Equal(1, message.FinalCalls);
        Assert.Same(error, message.FinalException);
        Assert.Equal(0, message.ExceptionCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EarlyDisposalOrHandlerFailure_ReleasesBlockedProducer(bool handlerFails)
    {
        await using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var message = new DuplexProbe { FailAfterFirst = handlerFails, FailFinal = true };
        var output = scope.ServiceProvider.GetRequiredService<IQueryMediator>()
            .StreamAsync(message, timeout.Token).GetAsyncEnumerator();
        try
        {
            var first = output.MoveNextAsync();
            await message.WriteAsync("a", timeout.Token);
            Assert.True(await first);
            await message.WriteAsync("b", timeout.Token);
            var blockedWrite = message.WriteAsync("c", timeout.Token).AsTask();
            Assert.False(blockedWrite.IsCompleted);

            if (handlerFails)
            {
                var error = await Assert.ThrowsAsync<InvalidOperationException>(async () => await output.MoveNextAsync());
                Assert.Equal("handler failed", error.Message);
            }
            else
                await output.DisposeAsync();

            Assert.True(message.HandlerDisposed.Task.IsCompleted);
            var producerError = await AssertProducerTerminated(() => blockedWrite);
            Assert.Equal(1, message.FinalCalls);
            Assert.Same(producerError, message.FinalException);
            Assert.Equal(0, message.ExceptionCalls);
            if (!handlerFails) Assert.IsType<StreamOutputDisposedException>(producerError);
            else Assert.Equal("handler failed", producerError.Message);
        }
        finally
        {
            timeout.Cancel();
            await output.DisposeAsync();
        }
    }

    [Fact]
    public async Task CancellationOfOutput_ClosesInputWriter()
    {
        await using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        using var cancellation = new CancellationTokenSource();
        var message = new DuplexProbe();
        await using var output = scope.ServiceProvider.GetRequiredService<IQueryMediator>()
            .StreamAsync(message, cancellation.Token).GetAsyncEnumerator();
        var read = output.MoveNextAsync();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await read);
        Assert.True(message.HandlerDisposed.Task.IsCompleted);
        Assert.Equal(StreamCompletion.Cancelled, message.Info.Completion);
        var error = await AssertProducerTerminated(() => message.WriteAsync("after cancellation").AsTask());
        Assert.IsAssignableFrom<OperationCanceledException>(error);
        Assert.Same(error, message.FinalException);
        Assert.Equal(1, message.FinalCalls);
    }

    [Fact]
    public async Task RefusedDispatch_ReleasesProducerWaitingBeforeHandlerStarts()
    {
        await using var provider = BuildProvider();
        await using var scope = provider.CreateAsyncScope();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var message = new DuplexProbe { Reject = true };
        await message.WriteAsync("a", timeout.Token);
        var blockedWrite = message.WriteAsync("b", timeout.Token).AsTask();
        await using var output = scope.ServiceProvider.GetRequiredService<IQueryMediator>()
            .StreamAsync(message, timeout.Token).GetAsyncEnumerator();
        try
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(async () => await output.MoveNextAsync());
            Assert.Equal("dispatch refused", error.Message);
            Assert.Same(error, await AssertProducerTerminated(() => blockedWrite));
            Assert.Same(error, message.FinalException);
            Assert.Equal(1, message.FinalCalls);
            Assert.Equal(0, message.ExceptionCalls);
        }
        finally { timeout.Cancel(); }
    }

    private static async Task<Exception> AssertProducerTerminated(Func<Task> write)
    {
        var error = await Record.ExceptionAsync(async () => await write().WaitAsync(TimeSpan.FromSeconds(1)));
        Assert.NotNull(error);
        Assert.False(error is TimeoutException, "Dispatch ended but the input producer remained blocked.");
        return error;
    }
}
