#pragma warning disable ERGOEXP003
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Abstractions.Streaming;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Stella.Ergosfare.Command.Test;

public sealed class StagePolicyInput : CommandStream<int, StagePolicyInput>, ICommand<int>
{
    public bool Fail { get; init; }
    public int PostCalls, ExceptionCalls, FinalCalls;
    public Exception? FinalError;
}

public sealed class StagePolicyHandler : ICommandHandler<StagePolicyInput, int>
{
    public async ValueTask<int> HandleAsync(StagePolicyInput message, ErgosfareContext context)
    {
        var sum = 0;
        await foreach (var item in message.WithCancellation(context.CancellationToken)) sum += item;
        if (message.Fail) throw new InvalidOperationException("stream failure");
        return sum;
    }
}

public sealed class StagePolicyPost : ICommandPostInterceptor<StagePolicyInput, int>
{
    public ValueTask<int> HandleAsync(StagePolicyInput message, int result, ErgosfareContext context)
    {
        message.PostCalls++;
        return new(result + 100);
    }
}

public sealed class StagePolicyException : ICommandExceptionInterceptor<StagePolicyInput, int>
{
    public ValueTask<int> HandleAsync(StagePolicyInput message, int result, Exception exception, ErgosfareContext context)
    {
        message.ExceptionCalls++;
        return new(-1);
    }
}

public sealed class StagePolicyFinal : ICommandFinalInterceptor<StagePolicyInput, int>
{
    public ValueTask HandleAsync(StagePolicyInput message, int result, Exception? exception, ErgosfareContext context)
    {
        message.FinalCalls++;
        message.FinalError = exception;
        return default;
    }
}

public class StreamStagePolicyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Category", "Unit")]
    public async Task StreamInput_ExcludesPostAndExceptionButKeepsFinal(bool fail)
    {
        await using var provider = new ServiceCollection().AddErgosfare(o => o.AddCommandModule(c => c
            .Register<StagePolicyHandler>().Register<StagePolicyPost>()
            .Register<StagePolicyException>().Register<StagePolicyFinal>())).BuildServiceProvider();
        await using var input = new StagePolicyInput { Fail = fail }.Pipe(Items());
        var mediator = provider.GetRequiredService<ICommandMediator>();
        if (fail)
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(async () => await mediator.SendAsync<int>(input));
            Assert.Same(error, input.FinalError);
        }
        else
        {
            Assert.Equal(3, await mediator.SendAsync<int>(input));
            Assert.Null(input.FinalError);
        }
        Assert.Equal(0, input.PostCalls);
        Assert.Equal(0, input.ExceptionCalls);
        Assert.Equal(1, input.FinalCalls);
    }

    private static async IAsyncEnumerable<int> Items()
    {
        yield return 1;
        await Task.Yield();
        yield return 2;
    }
}
