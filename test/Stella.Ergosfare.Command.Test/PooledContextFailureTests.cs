using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

namespace Stella.Ergosfare.Command.Test;

/// <summary>
/// What happens to the rented execution context when a dispatch does not end the ordinary
/// way: the pipeline throws before it ever returns a task, or it suspends and then fails.
/// Both arms exist so the context goes back to the pool anyway, and both were unreached.
/// </summary>
/// <remarks>
/// <para>
/// The three completions a pooled dispatch has are the inline one (a handler that finished
/// synchronously), the awaited one, and the throw — and only the first two are exercised by
/// dispatching ordinary handlers. A handler whose body is a <c>throw</c> expression never
/// yields a task at all, so the failure travels out of the executor's frame directly and the
/// only thing that can reclaim the context is the <c>catch</c> around the call.
/// </para>
/// <para>
/// Losing a context there would leak one per failure and nothing would ever report it. So
/// each case asserts on the instance the handler was handed: the exception reaches the
/// caller, and the context is reclaimed all the same.
/// </para>
/// </remarks>
public class PooledContextFailureTests
{
    public sealed class FastCommand : ICommand;

    public sealed class ExplodingCommand : ICommand;

    public sealed class ExplodingEcho : ICommand<string>;

    public sealed class SlowExplodingCommand : ICommand;

    private static ErgosfareContext? _seen;

    [ExcludeFromDiscovery]
    public sealed class FastCommandHandler : ICommandHandler<FastCommand>
    {
        public ValueTask HandleAsync(FastCommand command, ErgosfareContext context)
        {
            _seen = context;
            context.Set("pooled.marker", true);
            return ValueTask.CompletedTask;
        }
    }

    // Expression-bodied throw: no state machine, no task — the exception leaves through the
    // dispatch's own frame while the rented context is still in hand.
    [ExcludeFromDiscovery]
    public sealed class ExplodingCommandHandler : ICommandHandler<ExplodingCommand>
    {
        public ValueTask HandleAsync(ExplodingCommand command, ErgosfareContext context)
            => throw Recorded(context);
    }

    [ExcludeFromDiscovery]
    public sealed class ExplodingEchoHandler : ICommandHandler<ExplodingEcho, string>
    {
        public ValueTask<string> HandleAsync(ExplodingEcho command, ErgosfareContext context)
            => throw Recorded(context);
    }

    [ExcludeFromDiscovery]
    public sealed class SlowExplodingCommandHandler : ICommandHandler<SlowExplodingCommand>
    {
        public async ValueTask HandleAsync(SlowExplodingCommand command, ErgosfareContext context)
        {
            _seen = context;
            context.Set("pooled.marker", true);

            await Task.Yield();

            throw new InvalidOperationException("boom");
        }
    }

    private static InvalidOperationException Recorded(ErgosfareContext context)
    {
        _seen = context;
        context.Set("pooled.marker", true);

        return new InvalidOperationException("boom");
    }

    private static ServiceProvider Build()
        => new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c =>
            {
                c.Register<FastCommandHandler>();
                c.Register<ExplodingCommandHandler>();
                c.Register<ExplodingEchoHandler>();
                c.Register<SlowExplodingCommandHandler>();
            }))
            .BuildServiceProvider();

    /// <summary>The context the handler ran on is back in the pool, with nothing left on it.</summary>
    private static void AssertReclaimed()
    {
        var used = Assert.IsType<ErgosfareContext>(_seen);

        Assert.False(used.Has("pooled.marker"));
        Assert.False(used.CancellationToken.CanBeCanceled);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task TypedVoidDispatch_ReclaimsTheContextInline_WhenTheHandlerFinishesSynchronously()
    {
        await using var provider = Build();
        var engine = provider.GetRequiredService<MessageDispatchEngine>();
        using var cts = new CancellationTokenSource();
        _seen = null;

        await engine.DispatchVoidAsync(new FastCommand(), provider, cts.Token);

        // The fast path: no state machine was built, and the context came back in the same
        // frame that rented it.
        AssertReclaimed();
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task TypedVoidDispatch_ReclaimsTheContext_WhenThePipelineThrowsBeforeYieldingATask()
    {
        await using var provider = Build();
        var engine = provider.GetRequiredService<MessageDispatchEngine>();
        _seen = null;

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await engine.DispatchVoidAsync(new ExplodingCommand(), provider));

        Assert.Equal("boom", thrown.Message);
        AssertReclaimed();
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task TypedResultDispatch_ReclaimsTheContext_WhenThePipelineThrowsBeforeYieldingATask()
    {
        await using var provider = Build();
        var engine = provider.GetRequiredService<MessageDispatchEngine>();
        _seen = null;

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await engine.DispatchAsync<ExplodingEcho, string>(new ExplodingEcho(), provider));

        AssertReclaimed();
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task UntypedVoidDispatch_ReclaimsTheContext_WhenASuspendedPipelineFails()
    {
        await using var provider = Build();
        var engine = provider.GetRequiredService<MessageDispatchEngine>();
        _seen = null;

        // The awaited arm's failure path: the context is reclaimed from the helper's
        // finally rather than from the catch, and the caller still gets the exception.
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await engine.DispatchAsync(new SlowExplodingCommand(), provider));

        AssertReclaimed();
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task TypedVoidDispatch_ReclaimsTheContext_WhenASuspendedPipelineFails()
    {
        await using var provider = Build();
        var engine = provider.GetRequiredService<MessageDispatchEngine>();
        _seen = null;

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await engine.DispatchVoidAsync(new SlowExplodingCommand(), provider));

        AssertReclaimed();
    }
}
