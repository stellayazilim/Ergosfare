using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;

namespace Stella.Ergosfare.Command.Test;

/// <summary>
/// Dispatches whose pipeline genuinely suspends. Every other suite's handlers complete
/// synchronously, which is the engine's fast path — it returns the pooled context inline and
/// never builds a state machine. The other arm, where the context has to survive an await and
/// be returned from a <c>finally</c> on whatever thread the pipeline resumes on, was reached
/// by no test at all.
/// </summary>
/// <remarks>
/// <para>
/// What each case asserts is the pooling contract from the caller's side: a context is valid
/// only for the duration of its dispatch. The handler keeps the instance it was handed, and
/// once the dispatch has completed that instance must be back in the pool — observable as its
/// state being gone: items cleared, cancellation token reset.
/// </para>
/// <para>
/// A context that is <b>not</b> returned costs only an allocation and would fail nothing here;
/// one returned <b>twice</b>, or returned while still in use, hands a live dispatch's context
/// to the next one. The assertions are written so that state surviving the dispatch — the
/// symptom of the first bug — fails, and so that the handler's own view during the dispatch is
/// pinned separately from the caller's view after it.
/// </para>
/// </remarks>
public class SuspendedDispatchTests
{
    public sealed class SlowCommand : ICommand;

    public sealed class SlowEcho : ICommand<string>;

    /// <summary>What a handler saw while it ran, kept outside the context it is judging.</summary>
    private static class Observed
    {
        public static ErgosfareContext? Context;

        public static bool SawItem;

        public static bool SawToken;

        public static void Reset()
        {
            Context = null;
            SawItem = false;
            SawToken = false;
        }

        /// <summary>Records the context's state, then suspends so the pipeline really does.</summary>
        public static async ValueTask YieldOn(ErgosfareContext context)
        {
            context.Set("suspended.marker", true);

            Context = context;
            SawItem = context.Has("suspended.marker");
            SawToken = context.CancellationToken.CanBeCanceled;

            // Task.Yield never completes synchronously, so the dispatch above this frame
            // cannot take its IsCompletedSuccessfully shortcut.
            await Task.Yield();
        }
    }

    [ExcludeFromDiscovery]
    public sealed class SlowCommandHandler : ICommandHandler<SlowCommand>
    {
        public ValueTask HandleAsync(SlowCommand command, ErgosfareContext context)
            => Observed.YieldOn(context);
    }

    [ExcludeFromDiscovery]
    public sealed class SlowEchoHandler : ICommandHandler<SlowEcho, string>
    {
        public async ValueTask<string> HandleAsync(SlowEcho command, ErgosfareContext context)
        {
            await Observed.YieldOn(context);
            return "slow";
        }
    }

    private static ServiceProvider Build()
        => new ServiceCollection()
            .AddErgosfare(x => x.AddCommandModule(c =>
            {
                c.Register<SlowCommandHandler>();
                c.Register<SlowEchoHandler>();
            }))
            .BuildServiceProvider();

    /// <summary>
    /// The handler ran with a live context, and by the time the caller is back that context
    /// has been reclaimed — the two halves of the pooled dispatch's promise.
    /// </summary>
    private static void AssertRanAndWasReclaimed()
    {
        Assert.True(Observed.SawItem, "the handler could not read what it wrote on its own context");
        Assert.True(Observed.SawToken, "the dispatch's cancellation token did not reach the handler");

        var used = Assert.IsType<ErgosfareContext>(Observed.Context);

        Assert.False(used.Has("suspended.marker"));
        Assert.False(used.CancellationToken.CanBeCanceled);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task UntypedVoidDispatch_ReturnsThePooledContext_AfterSuspending()
    {
        await using var provider = Build();
        var engine = provider.GetRequiredService<MessageDispatchEngine>();
        using var cts = new CancellationTokenSource();
        Observed.Reset();

        await engine.DispatchAsync(new SlowCommand(), provider, cts.Token);

        AssertRanAndWasReclaimed();
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task TypedVoidDispatch_ReturnsThePooledContext_AfterSuspending()
    {
        await using var provider = Build();
        var engine = provider.GetRequiredService<MessageDispatchEngine>();
        using var cts = new CancellationTokenSource();
        Observed.Reset();

        await engine.DispatchVoidAsync(new SlowCommand(), provider, cts.Token);

        AssertRanAndWasReclaimed();
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task UntypedResultDispatch_ReturnsThePooledContext_AfterSuspending()
    {
        await using var provider = Build();
        var engine = provider.GetRequiredService<MessageDispatchEngine>();
        using var cts = new CancellationTokenSource();
        Observed.Reset();

        var result = await engine.DispatchAsync<string>(new SlowEcho(), provider, cts.Token);

        Assert.Equal("slow", result);
        AssertRanAndWasReclaimed();
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task TypedResultDispatch_ReturnsThePooledContext_AfterSuspending()
    {
        await using var provider = Build();
        var engine = provider.GetRequiredService<MessageDispatchEngine>();
        using var cts = new CancellationTokenSource();
        Observed.Reset();

        var result = await engine.DispatchAsync<SlowEcho, string>(new SlowEcho(), provider, cts.Token);

        Assert.Equal("slow", result);
        AssertRanAndWasReclaimed();
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task CallerOwnedContext_SurvivesASuspendedDispatch()
    {
        await using var provider = Build();
        var mediator = provider.GetRequiredService<ICommandMediator>();
        using var cts = new CancellationTokenSource();
        var context = new ErgosfareContext(cancellationToken: cts.Token);
        Observed.Reset();

        await mediator.SendAsync(new SlowCommand(), context);

        // The mirror image of the pooled cases: the caller owns this one, so nothing may
        // reclaim it — what the handler wrote is still readable after the dispatch.
        Assert.Same(context, Observed.Context);
        Assert.True(context.Has("suspended.marker"));
        Assert.True(context.CancellationToken.CanBeCanceled);
    }
}
