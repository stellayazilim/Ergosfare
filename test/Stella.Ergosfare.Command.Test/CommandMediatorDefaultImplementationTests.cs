using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;

namespace Stella.Ergosfare.Command.Test;

/// <summary>
/// The convenience overloads on <see cref="ICommandMediator"/> are default interface
/// implementations over the four full calls, so what they promise is a forwarding: which
/// lane each one lands in, and what the group filter has become by the time it gets there.
/// </summary>
/// <remarks>
/// <para>
/// These run against a mediator that implements the four full calls and <b>nothing else</b>,
/// which is the only way the default bodies execute at all — <c>CommandMediator</c> overrides
/// the typed ones, so dispatching through the shipped mediator proves nothing about them. It
/// is also the shape a third-party implementation has on the day it is written, which is who
/// the defaults exist for.
/// </para>
/// <para>
/// The distinction each assertion is really pinning: <see cref="GroupSet.Empty"/> arrives as
/// <c>null</c> (no filter, the default pipeline), while an empty <c>string[]</c> arrives as
/// itself — a filter that names no group. Two ways of saying "no groups" that do not mean the
/// same thing downstream.
/// </para>
/// </remarks>
public class CommandMediatorDefaultImplementationTests
{
    private static readonly GroupSet Reporting = GroupSet.Of("cmd.dim.reporting");

    // Never dispatched — the recorder below is the only receiver — so these are kept out of
    // the compiled closure, which is also what silences ERGO001 for a private marker type.
    [ExcludeFromDiscovery]
    private sealed class Ping : ICommand;

    [ExcludeFromDiscovery]
    private sealed class Ask : ICommand<string>;

    /// <summary>Which of the four full calls a convenience forwarded to.</summary>
    private enum Lane
    {
        None,
        Void,
        Result,
        VoidContext,
        ResultContext,
    }

    /// <summary>
    /// Implements exactly the four members <see cref="ICommandMediator"/> declares without a
    /// body, and records what each one was handed.
    /// </summary>
    private sealed class RecordingMediator : ICommandMediator
    {
        public Lane Landed { get; private set; }

        public object? Command { get; private set; }

        /// <summary>The forwarded filter, snapshotted — <c>null</c> is a distinct answer.</summary>
        public string[]? Groups { get; private set; }

        public CancellationToken Token { get; private set; }

        public ErgosfareContext? Context { get; private set; }

        /// <summary>What the result lanes answer with; read back to prove the value travels.</summary>
        public object Reply { get; init; } = "answered";

        public ValueTask SendAsync(ICommand command, IEnumerable<string>? groups,
            CancellationToken cancellationToken)
        {
            Record(Lane.Void, command, groups, cancellationToken, null);
            return ValueTask.CompletedTask;
        }

        public ValueTask<TResult> SendAsync<TResult>(ICommand<TResult> command, IEnumerable<string>? groups,
            CancellationToken cancellationToken)
        {
            Record(Lane.Result, command, groups, cancellationToken, null);
            return ValueTask.FromResult((TResult)Reply);
        }

        public ValueTask SendAsync(ICommand command, ErgosfareContext context, IEnumerable<string>? groups = null)
        {
            Record(Lane.VoidContext, command, groups, context.CancellationToken, context);
            return ValueTask.CompletedTask;
        }

        public ValueTask<TResult> SendAsync<TResult>(ICommand<TResult> command, ErgosfareContext context,
            IEnumerable<string>? groups = null)
        {
            Record(Lane.ResultContext, command, groups, context.CancellationToken, context);
            return ValueTask.FromResult((TResult)Reply);
        }

        private void Record(Lane lane, object command, IEnumerable<string>? groups, CancellationToken token,
            ErgosfareContext? context)
        {
            Landed = lane;
            Command = command;
            Groups = groups?.ToArray();
            Token = token;
            Context = context;
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task DefaultPipelineSends_ForwardWithNoFilter()
    {
        var recorder = new RecordingMediator();
        ICommandMediator mediator = recorder;
        using var cts = new CancellationTokenSource();
        var ping = new Ping();
        var ask = new Ask();

        await mediator.SendAsync(ping, cts.Token);

        Assert.Equal(Lane.Void, recorder.Landed);
        Assert.Same(ping, recorder.Command);
        Assert.Null(recorder.Groups);
        Assert.Equal(cts.Token, recorder.Token);

        // A result command binds the result overload even though it is also an ICommand:
        // ICommand<string> is the more derived parameter type.
        Assert.Equal("answered", await mediator.SendAsync(ask, cts.Token));

        Assert.Equal(Lane.Result, recorder.Landed);
        Assert.Same(ask, recorder.Command);
        Assert.Null(recorder.Groups);
        Assert.Equal(cts.Token, recorder.Token);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task GroupSetSends_ForwardTheNames_AndTheEmptySetAsNoFilter()
    {
        var recorder = new RecordingMediator();
        ICommandMediator mediator = recorder;

        await mediator.SendAsync(new Ping(), Reporting);

        Assert.Equal(Lane.Void, recorder.Landed);
        Assert.Equal(new[] { "cmd.dim.reporting" }, recorder.Groups);

        await mediator.SendAsync(new Ping(), GroupSet.Empty);

        // The empty set is not a filter naming nothing — it is the absence of a filter.
        Assert.Null(recorder.Groups);

        Assert.Equal("answered", await mediator.SendAsync(new Ask(), Reporting));

        Assert.Equal(Lane.Result, recorder.Landed);
        Assert.Equal(new[] { "cmd.dim.reporting" }, recorder.Groups);

        await mediator.SendAsync(new Ask(), GroupSet.Empty);

        Assert.Null(recorder.Groups);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task ArraySends_ForwardTheArrayItself_EmptyIncluded()
    {
        var recorder = new RecordingMediator();
        ICommandMediator mediator = recorder;

        await mediator.SendAsync(new Ping(), new[] { "cmd.dim.east", "cmd.dim.west" });

        Assert.Equal(Lane.Void, recorder.Landed);
        Assert.Equal(new[] { "cmd.dim.east", "cmd.dim.west" }, recorder.Groups);

        // Unlike GroupSet.Empty, an empty array stays an empty filter: the array overload
        // hands over what it was given without reading it.
        await mediator.SendAsync(new Ping(), Array.Empty<string>());

        Assert.NotNull(recorder.Groups);
        Assert.Empty(recorder.Groups!);

        Assert.Equal("answered", await mediator.SendAsync(new Ask(), new[] { "cmd.dim.east" }));

        Assert.Equal(Lane.Result, recorder.Landed);
        Assert.Equal(new[] { "cmd.dim.east" }, recorder.Groups);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task TypedSends_LandInTheResultLane_UnderEveryFilterShape()
    {
        var recorder = new RecordingMediator();
        ICommandMediator mediator = recorder;
        using var cts = new CancellationTokenSource();
        var ask = new Ask();

        // Full form: the type pair is the only difference from the untyped call, so the
        // default body must reach the same lane with the same filter.
        Assert.Equal("answered",
            await mediator.SendAsync<Ask, string>(ask, (IEnumerable<string>?)new[] { "cmd.dim.typed" }, cts.Token));

        Assert.Equal(Lane.Result, recorder.Landed);
        Assert.Same(ask, recorder.Command);
        Assert.Equal(new[] { "cmd.dim.typed" }, recorder.Groups);
        Assert.Equal(cts.Token, recorder.Token);

        Assert.Equal("answered", await mediator.SendAsync<Ask, string>(ask, cts.Token));

        Assert.Null(recorder.Groups);
        Assert.Equal(cts.Token, recorder.Token);

        Assert.Equal("answered", await mediator.SendAsync<Ask, string>(ask, Reporting));

        Assert.Equal(new[] { "cmd.dim.reporting" }, recorder.Groups);

        Assert.Equal("answered", await mediator.SendAsync<Ask, string>(ask, GroupSet.Empty));

        Assert.Null(recorder.Groups);

        Assert.Equal("answered", await mediator.SendAsync<Ask, string>(ask, new[] { "cmd.dim.array" }));

        Assert.Equal(new[] { "cmd.dim.array" }, recorder.Groups);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task TypedContextSend_ForwardsTheCallersContext()
    {
        var recorder = new RecordingMediator();
        ICommandMediator mediator = recorder;
        using var cts = new CancellationTokenSource();
        var context = new ErgosfareContext(cancellationToken: cts.Token);
        var ask = new Ask();

        Assert.Equal("answered",
            await mediator.SendAsync<Ask, string>(ask, context, new[] { "cmd.dim.nested" }));

        // The nested-dispatch path: the caller owns the context, so it must arrive as the
        // very instance passed — cancellation flows from it, not from an ambient token.
        Assert.Equal(Lane.ResultContext, recorder.Landed);
        Assert.Same(context, recorder.Context);
        Assert.Same(ask, recorder.Command);
        Assert.Equal(new[] { "cmd.dim.nested" }, recorder.Groups);
        Assert.Equal(cts.Token, recorder.Token);

        await mediator.SendAsync<Ask, string>(ask, context);

        Assert.Equal(Lane.ResultContext, recorder.Landed);
        Assert.Null(recorder.Groups);
    }
}
