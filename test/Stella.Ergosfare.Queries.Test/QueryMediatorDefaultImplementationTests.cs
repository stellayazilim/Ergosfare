using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Queries.Abstractions;

namespace Stella.Ergosfare.Queries.Test;

/// <summary>
/// The convenience overloads on <see cref="IQueryMediator"/> are default interface
/// implementations over the four full calls, so what they promise is a forwarding: which
/// lane each one lands in, and what the group filter has become by the time it gets there.
/// </summary>
/// <remarks>
/// <para>
/// These run against a mediator that implements the four full calls and <b>nothing else</b>,
/// which is the only way the default bodies execute at all — <c>QueryMediator</c> overrides
/// the typed ones, so dispatching through the shipped mediator proves nothing about them. It
/// is also the shape a third-party implementation has on the day it is written, which is who
/// the defaults exist for.
/// </para>
/// <para>
/// The distinction each assertion is really pinning: <see cref="GroupSet.Empty"/> arrives as
/// <c>null</c> (no filter, the default pipeline), while an empty <c>string[]</c> arrives as
/// itself — a filter that names no group.
/// </para>
/// <para>
/// The streaming conveniences are exercised through the same recorder. They are obsolete
/// pending the stream revision, not gone, and until they go they forward like every other
/// lane — the suppression below is the opt-in the notice asks for.
/// </para>
/// </remarks>
public class QueryMediatorDefaultImplementationTests
{
    private static readonly GroupSet Reporting = GroupSet.Of("qry.dim.reporting");

    // Never dispatched — the recorder below is the only receiver — so these are kept out of
    // the compiled closure, which is also what silences ERGO001 for a private marker type.
    [ExcludeFromDiscovery]
    private sealed class Ask : IQuery<string>;

    [ExcludeFromDiscovery]
    private sealed class Feed : IStreamQuery<string>;

    /// <summary>Which of the four full calls a convenience forwarded to.</summary>
    private enum Lane
    {
        None,
        Query,
        QueryContext,
        Stream,
        StreamContext,
    }

    /// <summary>
    /// Implements exactly the four members <see cref="IQueryMediator"/> declares without a
    /// body, and records what each one was handed.
    /// </summary>
    private sealed class RecordingMediator : IQueryMediator
    {
        public Lane Landed { get; private set; }

        public object? Query { get; private set; }

        /// <summary>The forwarded filter, snapshotted — <c>null</c> is a distinct answer.</summary>
        public string[]? Groups { get; private set; }

        public CancellationToken Token { get; private set; }

        public ErgosfareContext? Context { get; private set; }

        /// <summary>What the lanes answer with; read back to prove the value travels.</summary>
        public object Reply { get; init; } = "answered";

        public ValueTask<TQueryResult> QueryAsync<TQueryResult>(IQuery<TQueryResult> query,
            IEnumerable<string>? groups, CancellationToken cancellationToken)
        {
            Record(Lane.Query, query, groups, cancellationToken, null);
            return ValueTask.FromResult((TQueryResult)Reply);
        }

        public ValueTask<TQueryResult> QueryAsync<TQueryResult>(IQuery<TQueryResult> query,
            ErgosfareContext context, IEnumerable<string>? groups = null)
        {
            Record(Lane.QueryContext, query, groups, context.CancellationToken, context);
            return ValueTask.FromResult((TQueryResult)Reply);
        }

        [Obsolete(StreamRevision.Notice)]
        public IAsyncEnumerable<TQueryResult> StreamAsync<TQueryResult>(IStreamQuery<TQueryResult> query,
            IEnumerable<string>? groups, CancellationToken cancellationToken)
        {
            Record(Lane.Stream, query, groups, cancellationToken, null);
            return One<TQueryResult>();
        }

        [Obsolete(StreamRevision.Notice)]
        public IAsyncEnumerable<TQueryResult> StreamAsync<TQueryResult>(IStreamQuery<TQueryResult> query,
            ErgosfareContext context, IEnumerable<string>? groups = null)
        {
            Record(Lane.StreamContext, query, groups, context.CancellationToken, context);
            return One<TQueryResult>();
        }

        private async IAsyncEnumerable<TQueryResult> One<TQueryResult>()
        {
            await Task.Yield();
            yield return (TQueryResult)Reply;
        }

        private void Record(Lane lane, object query, IEnumerable<string>? groups, CancellationToken token,
            ErgosfareContext? context)
        {
            Landed = lane;
            Query = query;
            Groups = groups?.ToArray();
            Token = token;
            Context = context;
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task UntypedQueries_ForwardEveryFilterShape()
    {
        var recorder = new RecordingMediator();
        IQueryMediator mediator = recorder;
        using var cts = new CancellationTokenSource();
        var ask = new Ask();

        Assert.Equal("answered", await mediator.QueryAsync(ask, cts.Token));

        Assert.Equal(Lane.Query, recorder.Landed);
        Assert.Same(ask, recorder.Query);
        Assert.Null(recorder.Groups);
        Assert.Equal(cts.Token, recorder.Token);

        Assert.Equal("answered", await mediator.QueryAsync(ask, Reporting));

        Assert.Equal(new[] { "qry.dim.reporting" }, recorder.Groups);

        // The empty set is not a filter naming nothing — it is the absence of a filter.
        Assert.Equal("answered", await mediator.QueryAsync(ask, GroupSet.Empty));

        Assert.Null(recorder.Groups);

        Assert.Equal("answered", await mediator.QueryAsync(ask, new[] { "qry.dim.east" }));

        Assert.Equal(new[] { "qry.dim.east" }, recorder.Groups);

        // Unlike GroupSet.Empty, an empty array stays an empty filter: the array overload
        // hands over what it was given without reading it.
        Assert.Equal("answered", await mediator.QueryAsync(ask, Array.Empty<string>()));

        Assert.NotNull(recorder.Groups);
        Assert.Empty(recorder.Groups!);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task TypedQueries_LandInTheSameLane_UnderEveryFilterShape()
    {
        var recorder = new RecordingMediator();
        IQueryMediator mediator = recorder;
        using var cts = new CancellationTokenSource();
        var ask = new Ask();

        Assert.Equal("answered",
            await mediator.QueryAsync<Ask, string>(ask, (IEnumerable<string>?)new[] { "qry.dim.typed" }, cts.Token));

        Assert.Equal(Lane.Query, recorder.Landed);
        Assert.Same(ask, recorder.Query);
        Assert.Equal(new[] { "qry.dim.typed" }, recorder.Groups);
        Assert.Equal(cts.Token, recorder.Token);

        Assert.Equal("answered", await mediator.QueryAsync<Ask, string>(ask, cts.Token));

        Assert.Null(recorder.Groups);
        Assert.Equal(cts.Token, recorder.Token);

        Assert.Equal("answered", await mediator.QueryAsync<Ask, string>(ask, Reporting));

        Assert.Equal(new[] { "qry.dim.reporting" }, recorder.Groups);

        Assert.Equal("answered", await mediator.QueryAsync<Ask, string>(ask, GroupSet.Empty));

        Assert.Null(recorder.Groups);

        Assert.Equal("answered", await mediator.QueryAsync<Ask, string>(ask, new[] { "qry.dim.array" }));

        Assert.Equal(new[] { "qry.dim.array" }, recorder.Groups);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task TypedContextQuery_ForwardsTheCallersContext()
    {
        var recorder = new RecordingMediator();
        IQueryMediator mediator = recorder;
        using var cts = new CancellationTokenSource();
        var context = new ErgosfareContext(cancellationToken: cts.Token);
        var ask = new Ask();

        Assert.Equal("answered",
            await mediator.QueryAsync<Ask, string>(ask, context, new[] { "qry.dim.nested" }));

        // The nested-dispatch path: the caller owns the context, so it must arrive as the
        // very instance passed — cancellation flows from it, not from an ambient token.
        Assert.Equal(Lane.QueryContext, recorder.Landed);
        Assert.Same(context, recorder.Context);
        Assert.Equal(new[] { "qry.dim.nested" }, recorder.Groups);
        Assert.Equal(cts.Token, recorder.Token);

        Assert.Equal("answered", await mediator.QueryAsync<Ask, string>(ask, context));

        Assert.Equal(Lane.QueryContext, recorder.Landed);
        Assert.Null(recorder.Groups);
    }

#pragma warning disable CS0618 // the stream lanes are obsolete pending the revision, not gone
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Coverage")]
    public async Task StreamConveniences_ForwardEveryFilterShape()
    {
        var recorder = new RecordingMediator();
        IQueryMediator mediator = recorder;
        using var cts = new CancellationTokenSource();
        var feed = new Feed();

        Assert.Equal("answered", await Single(mediator.StreamAsync(feed, cts.Token)));

        // The convenience forwards eagerly — the recorder saw the call before anything was
        // enumerated — while the sequence itself is produced on the caller's pull.
        Assert.Equal(Lane.Stream, recorder.Landed);
        Assert.Same(feed, recorder.Query);
        Assert.Null(recorder.Groups);
        Assert.Equal(cts.Token, recorder.Token);

        Assert.Equal("answered", await Single(mediator.StreamAsync(feed, Reporting)));

        Assert.Equal(new[] { "qry.dim.reporting" }, recorder.Groups);

        Assert.Equal("answered", await Single(mediator.StreamAsync(feed, GroupSet.Empty)));

        Assert.Null(recorder.Groups);

        Assert.Equal("answered", await Single(mediator.StreamAsync(feed, new[] { "qry.dim.east" })));

        Assert.Equal(new[] { "qry.dim.east" }, recorder.Groups);
    }
#pragma warning restore CS0618

    private static async ValueTask<string> Single(IAsyncEnumerable<string> sequence)
    {
        await foreach (var item in sequence)
        {
            return item;
        }

        throw new InvalidOperationException("the sequence produced nothing");
    }
}
