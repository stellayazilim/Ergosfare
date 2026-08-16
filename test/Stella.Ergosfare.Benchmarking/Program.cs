using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
using Stella.Ergosfare.Core.Abstractions.DispatchRoots;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Events.Abstractions;
using Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Generated;
using Stella.Ergosfare.Queries.Abstractions;
using Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection;
using MediatR;

namespace Stella.Ergosfare.Benchmarking;

public class Program
{
    public static void Main(string[] args)
    {
        // A bare run keeps its historical meaning: the whole MediationBenchmark table.
        if (args.Length == 0)
        {
            BenchmarkRunner.Run<MediationBenchmark>();
            return;
        }

        // With arguments the switcher selects across the benchmark classes, so
        // `-- --filter *CachePressure*` reaches the measurement gate while
        // `-- --filter *Intercepted*` still reaches the mediation rows.
        BenchmarkSwitcher
            .FromTypes([typeof(MediationBenchmark), typeof(CachePressureBenchmark)])
            .Run(args);
    }
}

// ---------------------------------------------------------------------------
// Ergosfare messages & handlers
// ---------------------------------------------------------------------------

public sealed class VoidCommand : ICommand { }

public sealed class VoidCommandHandler : ICommandHandler<VoidCommand>
{
    public ValueTask HandleAsync(VoidCommand command, ErgosfareContext context) => ValueTask.CompletedTask;
}

public sealed class IntQuery : IQuery<int> { }

public sealed class IntQueryHandler : IQueryHandler<IntQuery, int>
{
    public ValueTask<int> HandleAsync(IntQuery query, ErgosfareContext context) => ValueTask.FromResult(7);
}

public sealed class PingEvent : IEvent { }

public sealed class FirstPingEventHandler : IEventHandler<PingEvent>
{
    public ValueTask HandleAsync(PingEvent @event, ErgosfareContext context) => ValueTask.CompletedTask;
}

public sealed class SecondPingEventHandler : IEventHandler<PingEvent>
{
    public ValueTask HandleAsync(PingEvent @event, ErgosfareContext context) => ValueTask.CompletedTask;
}

// Grouped variants on their own message types, so the grouped rows measure the grouped
// lane without changing the default rows' pipelines (a second handler on VoidCommand
// would suppress its compile-time plan, for instance).

public sealed class GroupedCommand : ICommand { }

[Group("bench")]
public sealed class GroupedCommandHandler : ICommandHandler<GroupedCommand>
{
    public ValueTask HandleAsync(GroupedCommand command, ErgosfareContext context) => ValueTask.CompletedTask;
}

public sealed class GroupedPingEvent : IEvent { }

[Group("bench")]
public sealed class FirstGroupedPingEventHandler : IEventHandler<GroupedPingEvent>
{
    public ValueTask HandleAsync(GroupedPingEvent @event, ErgosfareContext context) => ValueTask.CompletedTask;
}

[Group("bench")]
public sealed class SecondGroupedPingEventHandler : IEventHandler<GroupedPingEvent>
{
    public ValueTask HandleAsync(GroupedPingEvent @event, ErgosfareContext context) => ValueTask.CompletedTask;
}

// Intercepted variants on their own message types: one pass-through pre- and one
// pass-through post-interceptor each, so these rows measure the interceptor-bearing
// strategy path — the staged-plans epic baseline — without disturbing the default
// rows' interceptor-free fast lanes (interceptors bind to their message type only).

public sealed class InterceptedCommand : ICommand { }

public sealed class InterceptedCommandHandler : ICommandHandler<InterceptedCommand>
{
    public ValueTask HandleAsync(InterceptedCommand command, ErgosfareContext context) => ValueTask.CompletedTask;
}

public sealed class InterceptedCommandPreInterceptor : ICommandPreInterceptor<InterceptedCommand>
{
    public ValueTask<InterceptedCommand> HandleAsync(InterceptedCommand command, ErgosfareContext context)
        => ValueTask.FromResult(command);
}

public sealed class InterceptedCommandPostInterceptor : ICommandPostInterceptor<InterceptedCommand>
{
    public ValueTask<object> HandleAsync(InterceptedCommand command, object messageResult, ErgosfareContext context)
        => ValueTask.FromResult(messageResult);
}

public sealed class InterceptedIntQuery : IQuery<int> { }

public sealed class InterceptedIntQueryHandler : IQueryHandler<InterceptedIntQuery, int>
{
    public ValueTask<int> HandleAsync(InterceptedIntQuery query, ErgosfareContext context) => ValueTask.FromResult(7);
}

public sealed class InterceptedIntQueryPreInterceptor : IQueryPreInterceptor<InterceptedIntQuery>
{
    public ValueTask<InterceptedIntQuery> HandleAsync(InterceptedIntQuery query, ErgosfareContext context)
        => ValueTask.FromResult(query);
}

public sealed class InterceptedIntQueryPostInterceptor : IQueryPostInterceptor<InterceptedIntQuery, int>
{
    public ValueTask<int> HandleAsync(InterceptedIntQuery query, int queryResult, ErgosfareContext context)
        => ValueTask.FromResult(queryResult);
}

// The intercepted-event pair. Both carry the same pipeline shape — two handlers, one
// pass-through pre- and one pass-through post-interceptor — and differ in exactly one
// thing: the twin's handlers are nested types, which disqualifies a broadcast plan (the
// runtime orders pipeline segments by Type.FullName, whose '+' nesting separator sorts
// differently from the display name's dot, so the generator refuses to bake an order it
// cannot guarantee). Published through the same generated provider in the same process,
// the two rows are the plan/runtime A/B in one table — the shape that makes run-to-run
// drift structurally unable to separate them.

/// <summary>Shared no-op sink for the broadcast participants. Not optional: a plan bakes
/// its handler calls straight-line and devirtualized, so empty bodies would inline away and
/// the plan row would measure an empty publish. Both twins pay the same four increments.</summary>
public static class BroadcastTouch
{
    public static long Count;
}

public sealed class InterceptedPingEvent : IEvent { }

public sealed class FirstInterceptedPingEventHandler : IEventHandler<InterceptedPingEvent>
{
    public ValueTask HandleAsync(InterceptedPingEvent @event, ErgosfareContext context)
    {
        BroadcastTouch.Count++;
        return ValueTask.CompletedTask;
    }
}

public sealed class SecondInterceptedPingEventHandler : IEventHandler<InterceptedPingEvent>
{
    public ValueTask HandleAsync(InterceptedPingEvent @event, ErgosfareContext context)
    {
        BroadcastTouch.Count++;
        return ValueTask.CompletedTask;
    }
}

public sealed class InterceptedPingEventPreInterceptor : IEventPreInterceptor<InterceptedPingEvent>
{
    public ValueTask<InterceptedPingEvent> HandleAsync(InterceptedPingEvent @event, ErgosfareContext context)
    {
        BroadcastTouch.Count++;
        return ValueTask.FromResult(@event);
    }
}

public sealed class InterceptedPingEventPostInterceptor : IEventPostInterceptor<InterceptedPingEvent>
{
    public ValueTask HandleAsync(InterceptedPingEvent @event, ValueTask result, ErgosfareContext executionContext)
    {
        BroadcastTouch.Count++;
        return ValueTask.CompletedTask;
    }
}

public sealed class UnplannedPingEvent : IEvent { }

/// <summary>
/// The twin's handlers, nested for one reason: nesting is the cheapest disqualification
/// that leaves the pipeline itself identical. Everything else about this event matches
/// <see cref="InterceptedPingEvent"/>, so the two rows differ in which lane runs them and
/// nothing else.
/// </summary>
public static class UnplannedPingEventHandlers
{
    public sealed class First : IEventHandler<UnplannedPingEvent>
    {
        public ValueTask HandleAsync(UnplannedPingEvent @event, ErgosfareContext context)
        {
            BroadcastTouch.Count++;
            return ValueTask.CompletedTask;
        }
    }

    public sealed class Second : IEventHandler<UnplannedPingEvent>
    {
        public ValueTask HandleAsync(UnplannedPingEvent @event, ErgosfareContext context)
        {
            BroadcastTouch.Count++;
            return ValueTask.CompletedTask;
        }
    }
}

public sealed class UnplannedPingEventPreInterceptor : IEventPreInterceptor<UnplannedPingEvent>
{
    public ValueTask<UnplannedPingEvent> HandleAsync(UnplannedPingEvent @event, ErgosfareContext context)
    {
        BroadcastTouch.Count++;
        return ValueTask.FromResult(@event);
    }
}

public sealed class UnplannedPingEventPostInterceptor : IEventPostInterceptor<UnplannedPingEvent>
{
    public ValueTask HandleAsync(UnplannedPingEvent @event, ValueTask result, ErgosfareContext executionContext)
    {
        BroadcastTouch.Count++;
        return ValueTask.CompletedTask;
    }
}

// ---------------------------------------------------------------------------
// Five-participant pipeline scenario — the like-for-like interceptor/behavior
// comparison. Each library runs five participants with the same purposes on a
// result-bearing message: validate the message, rewrite the message, rewrite
// the result, recover from a handler exception (armed, never fires on the hot
// path), and an always-runs final touch. Each library uses its idiomatic
// construct (Ergosfare staged interceptors, MediatR/Mediator pipeline
// behaviors) and its default participant lifetime.
// ---------------------------------------------------------------------------

/// <summary>Shared no-op sink for the final/finally participants, so the JIT cannot
/// discard the finally blocks and every library pays the same touch.</summary>
public static class PipelineTouch
{
    public static long Count;
}

public sealed class PipelineIntQuery : IQuery<int>
{
    public int Value { get; set; } = 7;
}

public sealed class PipelineIntQueryHandler : IQueryHandler<PipelineIntQuery, int>
{
    public ValueTask<int> HandleAsync(PipelineIntQuery query, ErgosfareContext context)
        => ValueTask.FromResult(query.Value);
}

public sealed class PipelineValidationPreInterceptor : IQueryPreInterceptor<PipelineIntQuery>
{
    public ValueTask<PipelineIntQuery> HandleAsync(PipelineIntQuery query, ErgosfareContext context)
        => query.Value < 0
            ? throw new InvalidOperationException("negative value")
            : ValueTask.FromResult(query);
}

public sealed class PipelineRewritePreInterceptor : IQueryPreInterceptor<PipelineIntQuery>
{
    public ValueTask<PipelineIntQuery> HandleAsync(PipelineIntQuery query, ErgosfareContext context)
    {
        query.Value |= 1;
        return ValueTask.FromResult(query);
    }
}

public sealed class PipelineResultPostInterceptor : IQueryPostInterceptor<PipelineIntQuery, int>
{
    public ValueTask<int> HandleAsync(PipelineIntQuery query, int queryResult, ErgosfareContext context)
        => ValueTask.FromResult(queryResult + 1);
}

public sealed class PipelineRecoveryExceptionInterceptor : IQueryExceptionInterceptor<PipelineIntQuery, int>
{
    public ValueTask<int> HandleAsync(PipelineIntQuery query, int result, Exception exception, ErgosfareContext context)
        => ValueTask.FromResult(-1);
}

public sealed class PipelineFinalInterceptor : IQueryFinalInterceptor<PipelineIntQuery, int>
{
    public ValueTask HandleAsync(PipelineIntQuery query, int result, Exception? exception, ErgosfareContext context)
    {
        PipelineTouch.Count++;
        return ValueTask.CompletedTask;
    }
}

// ---------------------------------------------------------------------------
// Mediator (martinothamar/Mediator, source-generated) messages & handlers
// ---------------------------------------------------------------------------

public sealed class MediatorSgVoidCommand : Mediator.ICommand { }

public sealed class MediatorSgVoidCommandHandler : Mediator.ICommandHandler<MediatorSgVoidCommand>
{
    public ValueTask<Mediator.Unit> Handle(MediatorSgVoidCommand command, CancellationToken cancellationToken)
        => new(Mediator.Unit.Value);
}

public sealed class MediatorSgIntQuery : Mediator.IQuery<int> { }

public sealed class MediatorSgIntQueryHandler : Mediator.IQueryHandler<MediatorSgIntQuery, int>
{
    public ValueTask<int> Handle(MediatorSgIntQuery query, CancellationToken cancellationToken) => new(7);
}

public sealed class MediatorSgPingNotification : Mediator.INotification { }

public sealed class FirstMediatorSgPingHandler : Mediator.INotificationHandler<MediatorSgPingNotification>
{
    public ValueTask Handle(MediatorSgPingNotification notification, CancellationToken cancellationToken) => default;
}

public sealed class SecondMediatorSgPingHandler : Mediator.INotificationHandler<MediatorSgPingNotification>
{
    public ValueTask Handle(MediatorSgPingNotification notification, CancellationToken cancellationToken) => default;
}

// Mediator five-behavior pipeline counterparts (closed generics bound to this query
// only). Same outermost-first order as the MediatR set; the rewrite mutates rather
// than passing a new message, keeping the work identical across all three variants.

public sealed class MediatorSgPipelineIntQuery : Mediator.IQuery<int>
{
    public int Value { get; set; } = 7;
}

public sealed class MediatorSgPipelineIntQueryHandler : Mediator.IQueryHandler<MediatorSgPipelineIntQuery, int>
{
    public ValueTask<int> Handle(MediatorSgPipelineIntQuery query, CancellationToken cancellationToken)
        => new(query.Value);
}

public sealed class MediatorSgFinallyBehavior : Mediator.IPipelineBehavior<MediatorSgPipelineIntQuery, int>
{
    public async ValueTask<int> Handle(MediatorSgPipelineIntQuery message, Mediator.MessageHandlerDelegate<MediatorSgPipelineIntQuery, int> next, CancellationToken cancellationToken)
    {
        try
        {
            return await next(message, cancellationToken);
        }
        finally
        {
            PipelineTouch.Count++;
        }
    }
}

public sealed class MediatorSgRecoveryBehavior : Mediator.IPipelineBehavior<MediatorSgPipelineIntQuery, int>
{
    public async ValueTask<int> Handle(MediatorSgPipelineIntQuery message, Mediator.MessageHandlerDelegate<MediatorSgPipelineIntQuery, int> next, CancellationToken cancellationToken)
    {
        try
        {
            return await next(message, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return -1;
        }
    }
}

public sealed class MediatorSgValidationBehavior : Mediator.IPipelineBehavior<MediatorSgPipelineIntQuery, int>
{
    public ValueTask<int> Handle(MediatorSgPipelineIntQuery message, Mediator.MessageHandlerDelegate<MediatorSgPipelineIntQuery, int> next, CancellationToken cancellationToken)
        => message.Value < 0
            ? throw new InvalidOperationException("negative value")
            : next(message, cancellationToken);
}

public sealed class MediatorSgRewriteBehavior : Mediator.IPipelineBehavior<MediatorSgPipelineIntQuery, int>
{
    public ValueTask<int> Handle(MediatorSgPipelineIntQuery message, Mediator.MessageHandlerDelegate<MediatorSgPipelineIntQuery, int> next, CancellationToken cancellationToken)
    {
        message.Value |= 1;
        return next(message, cancellationToken);
    }
}

public sealed class MediatorSgResultRewriteBehavior : Mediator.IPipelineBehavior<MediatorSgPipelineIntQuery, int>
{
    public async ValueTask<int> Handle(MediatorSgPipelineIntQuery message, Mediator.MessageHandlerDelegate<MediatorSgPipelineIntQuery, int> next, CancellationToken cancellationToken)
        => await next(message, cancellationToken) + 1;
}

// ---------------------------------------------------------------------------
// MediatR requests & handlers
// ---------------------------------------------------------------------------

public sealed class MediatrVoidRequest : IRequest { }

public sealed class MediatrVoidRequestHandler : IRequestHandler<MediatrVoidRequest>
{
    public Task Handle(MediatrVoidRequest request, CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class MediatrIntRequest : IRequest<int> { }

public sealed class MediatrIntRequestHandler : IRequestHandler<MediatrIntRequest, int>
{
    public Task<int> Handle(MediatrIntRequest request, CancellationToken cancellationToken) => Task.FromResult(7);
}

public sealed class MediatrPingNotification : INotification { }

public sealed class FirstMediatrPingHandler : INotificationHandler<MediatrPingNotification>
{
    public Task Handle(MediatrPingNotification notification, CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class SecondMediatrPingHandler : INotificationHandler<MediatrPingNotification>
{
    public Task Handle(MediatrPingNotification notification, CancellationToken cancellationToken) => Task.CompletedTask;
}

// MediatR five-behavior pipeline counterparts (closed generics, so they bind to this
// request only and leave the plain rows' pipelines untouched). Registration order is
// outermost-first: finally, recovery, validation, rewrite, result rewrite — mirroring
// the stage positions of the Ergosfare scenario. MediatR cannot replace the request
// through RequestHandlerDelegate, so the rewrite mutates, as all three variants do.

public sealed class MediatrPipelineIntRequest : IRequest<int>
{
    public int Value { get; set; } = 7;
}

public sealed class MediatrPipelineIntRequestHandler : IRequestHandler<MediatrPipelineIntRequest, int>
{
    public Task<int> Handle(MediatrPipelineIntRequest request, CancellationToken cancellationToken)
        => Task.FromResult(request.Value);
}

public sealed class MediatrFinallyBehavior : IPipelineBehavior<MediatrPipelineIntRequest, int>
{
    public async Task<int> Handle(MediatrPipelineIntRequest request, RequestHandlerDelegate<int> next, CancellationToken cancellationToken)
    {
        try
        {
            return await next();
        }
        finally
        {
            PipelineTouch.Count++;
        }
    }
}

public sealed class MediatrRecoveryBehavior : IPipelineBehavior<MediatrPipelineIntRequest, int>
{
    public async Task<int> Handle(MediatrPipelineIntRequest request, RequestHandlerDelegate<int> next, CancellationToken cancellationToken)
    {
        try
        {
            return await next();
        }
        catch (InvalidOperationException)
        {
            return -1;
        }
    }
}

public sealed class MediatrValidationBehavior : IPipelineBehavior<MediatrPipelineIntRequest, int>
{
    public Task<int> Handle(MediatrPipelineIntRequest request, RequestHandlerDelegate<int> next, CancellationToken cancellationToken)
        => request.Value < 0
            ? throw new InvalidOperationException("negative value")
            : next();
}

public sealed class MediatrRewriteBehavior : IPipelineBehavior<MediatrPipelineIntRequest, int>
{
    public Task<int> Handle(MediatrPipelineIntRequest request, RequestHandlerDelegate<int> next, CancellationToken cancellationToken)
    {
        request.Value |= 1;
        return next();
    }
}

public sealed class MediatrResultRewriteBehavior : IPipelineBehavior<MediatrPipelineIntRequest, int>
{
    public async Task<int> Handle(MediatrPipelineIntRequest request, RequestHandlerDelegate<int> next, CancellationToken cancellationToken)
        => await next() + 1;
}

/// <summary>
/// Two scenarios, grouped as table categories:
/// <para><b>Root</b> — mediators resolved once from the root provider (singleton-style
/// usage: background workers, message-pump loops).</para>
/// <para><b>Scoped</b> — a fresh DI scope per dispatch (the web-request shape; includes
/// scope creation and mediator resolution in every measurement).</para>
/// Every row dispatches through a public mediator facade — the surface an application
/// actually calls; the internal engine is not benchmarked separately, since nothing but
/// the facades reaches it. Each category's baseline is Ergosfare's plain command
/// dispatch, so the Ratio column reads as "what does this shape, lane or library cost
/// relative to a bare command". Within each category: a void dispatch, a
/// result-returning dispatch, a five-participant pipeline and a two-handler event
/// publish, for Ergosfare and the competitors alike.
/// <para>Competitors run their out-of-the-box defaults: MediatR (reflection-based,
/// transient handlers) as the ubiquitous baseline, and martinothamar/Mediator
/// (source-generated dispatch, singleton lifetime by default) as the fastest widely-used
/// alternative — the <c>MediatorSg_*</c> rows. Ergosfare's default rows resolve transient
/// handlers per dispatch; the <c>Command_Void_Memoized</c> row shows the zero-allocation
/// shape <c>ForceMemoizedHandlers()</c> (or plain singleton handler registration) yields,
/// which is the apples-to-apples comparison against Mediator's singleton default.</para>
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class MediationBenchmark
{
    private ServiceProvider _ergosfare = null!;
    private ServiceProvider _ergosfareGenerated = null!;
    private ServiceProvider _ergosfareMemoized = null!;
    private ServiceProvider _mediatr = null!;
    private ServiceProvider _mediatorSg = null!;

    private ICommandMediator _commands = null!;
    private ICommandMediator _generatedCommands = null!;
    private ICommandMediator _memoizedCommands = null!;
    private IQueryMediator _memoizedQueries = null!;
    private IQueryMediator _queries = null!;
    private IQueryMediator _generatedQueries = null!;
    private IEventMediator _events = null!;
    private IEventMediator _generatedEvents = null!;
    private IMediator _mediator = null!;
    private Mediator.IMediator _martinMediator = null!;

    private readonly VoidCommand _voidCommand = new();
    private readonly IntQuery _intQuery = new();
    private readonly PingEvent _pingEvent = new();
    private readonly GroupedCommand _groupedCommand = new();
    private readonly GroupedPingEvent _groupedPingEvent = new();
    private readonly InterceptedCommand _interceptedCommand = new();
    private readonly InterceptedIntQuery _interceptedIntQuery = new();
    private readonly InterceptedPingEvent _interceptedPingEvent = new();
    private readonly UnplannedPingEvent _unplannedPingEvent = new();
    private readonly PipelineIntQuery _pipelineIntQuery = new();
    private readonly MediatrPipelineIntRequest _mediatrPipelineInt = new();
    private readonly MediatorSgPipelineIntQuery _mediatorSgPipelineInt = new();

    private static readonly string[] BenchGroups = ["bench"];
    private static readonly GroupSet BenchGroupSet = GroupSet.Of("bench");

    // Reused across dispatches, mirroring a caller that keeps its settings: the grouped
    // rows measure the grouped lane itself, not per-call settings construction.
    private readonly MediatrVoidRequest _mediatrVoid = new();
    private readonly MediatrIntRequest _mediatrInt = new();
    private readonly MediatrPingNotification _mediatrPing = new();
    private readonly MediatorSgVoidCommand _mediatorSgVoid = new();
    private readonly MediatorSgIntQuery _mediatorSgInt = new();
    private readonly MediatorSgPingNotification _mediatorSgPing = new();

    [GlobalSetup]
    public void Setup()
    {
        _ergosfare = new ServiceCollection()
            .AddErgosfare(options =>
            {
                options.AddCommandModule(commands =>
                {
                    commands.Register<VoidCommandHandler>();
                    commands.Register<GroupedCommandHandler>();
                    commands.Register<InterceptedCommandHandler>();
                    commands.Register<InterceptedCommandPreInterceptor>();
                    commands.Register<InterceptedCommandPostInterceptor>();
                });
                options.AddQueryModule(queries =>
                {
                    queries.Register<IntQueryHandler>();
                    queries.Register<InterceptedIntQueryHandler>();
                    queries.Register<InterceptedIntQueryPreInterceptor>();
                    queries.Register<InterceptedIntQueryPostInterceptor>();
                    queries.Register<PipelineIntQueryHandler>();
                    queries.Register<PipelineValidationPreInterceptor>();
                    queries.Register<PipelineRewritePreInterceptor>();
                    queries.Register<PipelineResultPostInterceptor>();
                    queries.Register<PipelineRecoveryExceptionInterceptor>();
                    queries.Register<PipelineFinalInterceptor>();
                });
                options.AddEventModule(events =>
                {
                    events.Register<FirstPingEventHandler>();
                    events.Register<SecondPingEventHandler>();
                    events.Register<FirstGroupedPingEventHandler>();
                    events.Register<SecondGroupedPingEventHandler>();
                    events.Register<FirstInterceptedPingEventHandler>();
                    events.Register<SecondInterceptedPingEventHandler>();
                    events.Register<InterceptedPingEventPreInterceptor>();
                    events.Register<InterceptedPingEventPostInterceptor>();
                });
            })
            .BuildServiceProvider();

        _commands = _ergosfare.GetRequiredService<ICommandMediator>();
        _queries = _ergosfare.GetRequiredService<IQueryMediator>();
        _events = _ergosfare.GetRequiredService<IEventMediator>();

        _mediatr = new ServiceCollection()
            .AddMediatR(configuration =>
            {
                configuration.RegisterServicesFromAssembly(typeof(MediationBenchmark).Assembly);
                // Closed behaviors for the five-participant scenario, outermost first.
                configuration.AddBehavior<IPipelineBehavior<MediatrPipelineIntRequest, int>, MediatrFinallyBehavior>();
                configuration.AddBehavior<IPipelineBehavior<MediatrPipelineIntRequest, int>, MediatrRecoveryBehavior>();
                configuration.AddBehavior<IPipelineBehavior<MediatrPipelineIntRequest, int>, MediatrValidationBehavior>();
                configuration.AddBehavior<IPipelineBehavior<MediatrPipelineIntRequest, int>, MediatrRewriteBehavior>();
                configuration.AddBehavior<IPipelineBehavior<MediatrPipelineIntRequest, int>, MediatrResultRewriteBehavior>();
            })
            .BuildServiceProvider();

        _mediator = _mediatr.GetRequiredService<IMediator>();

        _mediatorSg = new ServiceCollection()
            .AddMediator()
            // Closed behaviors for the five-participant scenario, outermost first.
            // Singleton matches the library's default lifetime model.
            .AddSingleton<Mediator.IPipelineBehavior<MediatorSgPipelineIntQuery, int>, MediatorSgFinallyBehavior>()
            .AddSingleton<Mediator.IPipelineBehavior<MediatorSgPipelineIntQuery, int>, MediatorSgRecoveryBehavior>()
            .AddSingleton<Mediator.IPipelineBehavior<MediatorSgPipelineIntQuery, int>, MediatorSgValidationBehavior>()
            .AddSingleton<Mediator.IPipelineBehavior<MediatorSgPipelineIntQuery, int>, MediatorSgRewriteBehavior>()
            .AddSingleton<Mediator.IPipelineBehavior<MediatorSgPipelineIntQuery, int>, MediatorSgResultRewriteBehavior>()
            .BuildServiceProvider();

        _martinMediator = _mediatorSg.GetRequiredService<Mediator.IMediator>();

        // The five-participant rows only compare fairly if every library actually ran
        // all five: (7 | 1) + 1 = 8, and each dispatch touches the finally sink once.
        AssertPipelineScenario(() => _queries.QueryAsync(_pipelineIntQuery).AsTask().GetAwaiter().GetResult(), "Ergosfare");
        AssertPipelineScenario(() => _mediator.Send(_mediatrPipelineInt).GetAwaiter().GetResult(), "MediatR");
        AssertPipelineScenario(() => _martinMediator.Send(_mediatorSgPipelineInt).AsTask().GetAwaiter().GetResult(), "Mediator");

        AssertBroadcastScenario(() => _events.PublishAsync(_interceptedPingEvent), "Event_Publish_Intercepted");
    }

    private static void AssertPipelineScenario(Func<int> dispatch, string library)
    {
        var touchesBefore = PipelineTouch.Count;
        var result = dispatch();
        if (result != 8)
            throw new InvalidOperationException($"{library} five-participant pipeline returned {result}, expected 8 — a participant did not run.");
        if (PipelineTouch.Count != touchesBefore + 1)
            throw new InvalidOperationException($"{library} five-participant pipeline did not run its final participant.");
    }

    /// <summary>
    /// The intercepted-broadcast rows only mean anything if all four participants ran: a
    /// pre-interceptor, both handlers and a post-interceptor. A twin that silently lost a
    /// handler — a nested type the registration skipped, say — would publish to a shorter
    /// pipeline and read as a win that never happened.
    /// </summary>
    private static void AssertBroadcastScenario(Func<ValueTask> publish, string row)
    {
        var touchesBefore = BroadcastTouch.Count;
        publish().AsTask().GetAwaiter().GetResult();
        var touches = BroadcastTouch.Count - touchesBefore;
        if (touches != 4)
            throw new InvalidOperationException($"{row} touched {touches} participants, expected 4 — the pipeline is not the shape this row claims to measure.");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _ergosfare.Dispose();
        _mediatr.Dispose();
        _mediatorSg.Dispose();
    }

    /// <summary>
    /// Source-generated registration variant, isolated to its own benchmark process via
    /// targets: <c>RegisterGenerated()</c> installs the compile-time dispatch roots and
    /// void pipeline plans process-wide, and the targeted setup keeps that installation
    /// away from the runtime-registration rows' processes so the comparison stays honest.
    /// </summary>
    [GlobalSetup(Targets = [nameof(Command_Void_Generated), nameof(Query_Result_Generated),
        nameof(Command_Void_Intercepted_Generated), nameof(Query_Result_Intercepted_Generated),
        nameof(Query_Result_Pipeline5_Generated), nameof(Event_Publish_Intercepted_Generated),
        nameof(Event_Publish_Intercepted_Unplanned)])]
    public void SetupGenerated()
    {
        _ergosfareGenerated = new ServiceCollection()
            .AddErgosfare(options =>
            {
                options.AddCommandModule(commands => commands.RegisterGenerated());
                options.AddQueryModule(queries => queries.RegisterGenerated());
                options.AddEventModule(events => events.RegisterGenerated());
            })
            .BuildServiceProvider();

        _generatedCommands = _ergosfareGenerated.GetRequiredService<ICommandMediator>();
        _generatedQueries = _ergosfareGenerated.GetRequiredService<IQueryMediator>();
        _generatedEvents = _ergosfareGenerated.GetRequiredService<IEventMediator>();

        AssertPipelineScenario(
            () => _generatedQueries.QueryAsync(_pipelineIntQuery).AsTask().GetAwaiter().GetResult(),
            "Ergosfare (generated)");

        // The A/B's premise, asserted rather than assumed: one twin has a compiled
        // broadcast plan and the other does not. Without this the pair degrades silently
        // into two rows measuring the same lane — the failure mode that makes an A/B
        // table read as "the plan bought nothing".
        if (GeneratedDispatchRoots.FindBroadcastPlan(typeof(InterceptedPingEvent)) is null)
            throw new InvalidOperationException(
                $"{nameof(Event_Publish_Intercepted_Generated)} lost its plan arm: no broadcast plan was emitted for {nameof(InterceptedPingEvent)}.");

        if (GeneratedDispatchRoots.FindBroadcastPlan(typeof(UnplannedPingEvent)) is not null)
            throw new InvalidOperationException(
                $"{nameof(Event_Publish_Intercepted_Unplanned)} lost its runtime arm: {nameof(UnplannedPingEvent)} got a broadcast plan, so both A/B rows measure the plan.");

        AssertBroadcastScenario(() => _generatedEvents.PublishAsync(_interceptedPingEvent), nameof(Event_Publish_Intercepted_Generated));
        AssertBroadcastScenario(() => _generatedEvents.PublishAsync(_unplannedPingEvent), nameof(Event_Publish_Intercepted_Unplanned));
    }

    [GlobalCleanup(Targets = [nameof(Command_Void_Generated), nameof(Query_Result_Generated),
        nameof(Command_Void_Intercepted_Generated), nameof(Query_Result_Intercepted_Generated),
        nameof(Query_Result_Pipeline5_Generated), nameof(Event_Publish_Intercepted_Generated),
        nameof(Event_Publish_Intercepted_Unplanned)])]
    public void CleanupGenerated()
    {
        _ergosfareGenerated.Dispose();
    }

    /// <summary>
    /// Zero-allocation variant: <c>ForceMemoizedHandlers()</c> resolves each handler graph
    /// once and reuses it for every dispatch, so the transient handler instance — the last
    /// 24 B on the default root path — disappears. Plain singleton handler registration
    /// reaches the same shape without the switch. Isolated to its own process via targets,
    /// like the generated variant above.
    /// </summary>
    [GlobalSetup(Targets = [nameof(Command_Void_Memoized), nameof(Query_Result_Memoized),
        nameof(Query_Result_Pipeline5_Memoized)])]
    public void SetupMemoized()
    {
        _ergosfareMemoized = new ServiceCollection()
            .AddErgosfare(options =>
            {
                options.ForceMemoizedHandlers();
                options.AddCommandModule(commands => commands.Register<VoidCommandHandler>());
                options.AddQueryModule(queries =>
                {
                    queries.Register<IntQueryHandler>();
                    queries.Register<PipelineIntQueryHandler>();
                    queries.Register<PipelineValidationPreInterceptor>();
                    queries.Register<PipelineRewritePreInterceptor>();
                    queries.Register<PipelineResultPostInterceptor>();
                    queries.Register<PipelineRecoveryExceptionInterceptor>();
                    queries.Register<PipelineFinalInterceptor>();
                });
            })
            .BuildServiceProvider();

        _memoizedCommands = _ergosfareMemoized.GetRequiredService<ICommandMediator>();
        _memoizedQueries = _ergosfareMemoized.GetRequiredService<IQueryMediator>();

        AssertPipelineScenario(
            () => _memoizedQueries.QueryAsync(_pipelineIntQuery).AsTask().GetAwaiter().GetResult(),
            "Ergosfare (memoized)");
    }

    [GlobalCleanup(Targets = [nameof(Command_Void_Memoized), nameof(Query_Result_Memoized),
        nameof(Query_Result_Pipeline5_Memoized)])]
    public void CleanupMemoized()
    {
        _ergosfareMemoized.Dispose();
    }

    // ------------------------------------------------------------------
    // Root — mediators resolved once, no per-dispatch scope
    // ------------------------------------------------------------------

    [Benchmark(Baseline = true), BenchmarkCategory("Root")]
    public ValueTask Command_Void() => _commands.SendAsync(_voidCommand);

    [Benchmark, BenchmarkCategory("Root")]
    public ValueTask Command_Void_Generated() => _generatedCommands.SendAsync(_voidCommand);

    [Benchmark, BenchmarkCategory("Root")]
    public ValueTask Command_Void_Memoized() => _memoizedCommands.SendAsync(_voidCommand);

    /// <summary>
    /// The interceptor-bearing strategy path (one pass-through pre- and post-interceptor):
    /// the baseline the staged-plans epic optimizes. Every dispatch that can't take a fast
    /// lane pays this shape.
    /// </summary>
    [Benchmark, BenchmarkCategory("Root")]
    public ValueTask Command_Void_Intercepted() => _commands.SendAsync(_interceptedCommand);

    [Benchmark, BenchmarkCategory("Root")]
    public ValueTask<int> Query_Result() => _queries.QueryAsync(_intQuery);

    [Benchmark, BenchmarkCategory("Root")]
    public ValueTask<int> Query_Result_Intercepted() => _queries.QueryAsync(_interceptedIntQuery);

    [Benchmark, BenchmarkCategory("Root")]
    public ValueTask<int> Query_Result_Generated() => _generatedQueries.QueryAsync(_intQuery);

    /// <summary>
    /// The staged-plan lane: the same interceptor-bearing pipelines as the
    /// <c>*_Intercepted</c> rows, dispatched through the generated provider whose
    /// <c>RegisterGenerated</c> installed bespoke staged plans — the strategy machinery
    /// those baseline rows pay for is replaced by straight-line emitted code.
    /// </summary>
    [Benchmark, BenchmarkCategory("Root")]
    public ValueTask Command_Void_Intercepted_Generated() => _generatedCommands.SendAsync(_interceptedCommand);

    [Benchmark, BenchmarkCategory("Root")]
    public ValueTask<int> Query_Result_Intercepted_Generated() => _generatedQueries.QueryAsync(_interceptedIntQuery);

    // ------------------------------------------------------------------
    // Five-participant pipeline — the like-for-like comparison: five
    // equivalent-purpose participants per library (validate, rewrite message,
    // rewrite result, exception recovery, final touch) on a result-bearing
    // message, each library in its idiomatic construct and default lifetime.
    // ------------------------------------------------------------------

    /// <summary>Runtime-registered interceptors on the strategy path — the pre-staged-plans shape.</summary>
    [Benchmark, BenchmarkCategory("Root")]
    public ValueTask<int> Query_Result_Pipeline5() => _queries.QueryAsync(_pipelineIntQuery);

    /// <summary>The same five interceptors dispatched through the emitted staged plan.</summary>
    [Benchmark, BenchmarkCategory("Root")]
    public ValueTask<int> Query_Result_Pipeline5_Generated() => _generatedQueries.QueryAsync(_pipelineIntQuery);

    /// <summary>Five closed-generic MediatR behaviors with the equivalent purposes.</summary>
    [Benchmark, BenchmarkCategory("Root")]
    public Task<int> MediatR_Send_Result_Pipeline5() => _mediator.Send(_mediatrPipelineInt);

    /// <summary>Five closed-generic Mediator behaviors with the equivalent purposes.</summary>
    [Benchmark, BenchmarkCategory("Root")]
    public ValueTask<int> MediatorSg_Send_Result_Pipeline5() => _martinMediator.Send(_mediatorSgPipelineInt);

    // ------------------------------------------------------------------
    // Singleton-path rows — the apples-to-apples comparison against
    // Mediator's singleton default: ForceMemoizedHandlers resolves each
    // participant graph once and reuses it, which is Ergosfare's
    // singleton-equivalent shape.
    // ------------------------------------------------------------------

    [Benchmark, BenchmarkCategory("Root")]
    public ValueTask<int> Query_Result_Memoized() => _memoizedQueries.QueryAsync(_intQuery);

    /// <summary>The five-participant pipeline with memoized participants. Memoized
    /// compositions run the strategy path — the staged-plan gate deliberately steps
    /// aside for them — so this row also measures that open optimization gap.</summary>
    [Benchmark, BenchmarkCategory("Root")]
    public ValueTask<int> Query_Result_Pipeline5_Memoized() => _memoizedQueries.QueryAsync(_pipelineIntQuery);

    [Benchmark, BenchmarkCategory("Root")]
    public ValueTask Command_Void_Grouped() => _commands.SendAsync(_groupedCommand, BenchGroups);

    /// <summary>
    /// The canonical-filter overload: no settings object, and the grouped executor
    /// lookup matches the reused <see cref="GroupSet"/> on a single reference check.
    /// </summary>
    [Benchmark, BenchmarkCategory("Root")]
    public ValueTask Command_Void_Grouped_GroupSet() => _commands.SendAsync(_groupedCommand, BenchGroupSet);

    [Benchmark, BenchmarkCategory("Root")]
    public ValueTask Event_Publish() => _events.PublishAsync(_pingEvent);

    /// <summary>
    /// The interceptor-bearing publish on the runtime strategy — today's shape for an
    /// intercepted event, and the number the broadcast plan has to beat. The plain
    /// <see cref="Event_Publish"/> row above never reaches this lane: with no interceptor
    /// it takes the straight-through publish instead.
    /// </summary>
    [Benchmark, BenchmarkCategory("Root")]
    public ValueTask Event_Publish_Intercepted() => _events.PublishAsync(_interceptedPingEvent);

    /// <summary>
    /// The A/B's plan arm: the same intercepted publish through the generated provider,
    /// where a compiled broadcast plan serves it.
    /// </summary>
    [Benchmark, BenchmarkCategory("Root")]
    public ValueTask Event_Publish_Intercepted_Generated() => _generatedEvents.PublishAsync(_interceptedPingEvent);

    /// <summary>
    /// The A/B's runtime arm: the identical pipeline whose nested handlers disqualified a
    /// plan, dispatched through the very same provider in the very same process. The pair
    /// is read as a ratio within one table — never across runs.
    /// </summary>
    [Benchmark, BenchmarkCategory("Root")]
    public ValueTask Event_Publish_Intercepted_Unplanned() => _generatedEvents.PublishAsync(_unplannedPingEvent);

    [Benchmark, BenchmarkCategory("Root")]
    public ValueTask Event_Publish_Grouped() => _events.PublishAsync(_groupedPingEvent, BenchGroups);

    [Benchmark, BenchmarkCategory("Root")]
    public ValueTask Event_Publish_Grouped_GroupSet() => _events.PublishAsync(_groupedPingEvent, BenchGroupSet);

    [Benchmark, BenchmarkCategory("Root")]
    public Task MediatR_Send_Void() => _mediator.Send(_mediatrVoid);

    [Benchmark, BenchmarkCategory("Root")]
    public Task<int> MediatR_Send_Result() => _mediator.Send(_mediatrInt);

    [Benchmark, BenchmarkCategory("Root")]
    public Task MediatR_Publish() => _mediator.Publish(_mediatrPing);

    [Benchmark, BenchmarkCategory("Root")]
    public ValueTask<Mediator.Unit> MediatorSg_Send_Void() => _martinMediator.Send(_mediatorSgVoid);

    [Benchmark, BenchmarkCategory("Root")]
    public ValueTask<int> MediatorSg_Send_Result() => _martinMediator.Send(_mediatorSgInt);

    [Benchmark, BenchmarkCategory("Root")]
    public ValueTask MediatorSg_Publish() => _martinMediator.Publish(_mediatorSgPing);

    // ------------------------------------------------------------------
    // Scoped — a fresh DI scope and mediator resolution per dispatch
    // ------------------------------------------------------------------

    [Benchmark(Baseline = true), BenchmarkCategory("Scoped")]
    public async Task Command_Void_Scoped()
    {
        using var scope = _ergosfare.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ICommandMediator>().SendAsync(_voidCommand);
    }

    [Benchmark, BenchmarkCategory("Scoped")]
    public async Task<int> Query_Result_Scoped()
    {
        using var scope = _ergosfare.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IQueryMediator>().QueryAsync(_intQuery);
    }

    [Benchmark, BenchmarkCategory("Scoped")]
    public async Task Event_Publish_Scoped()
    {
        using var scope = _ergosfare.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IEventMediator>().PublishAsync(_pingEvent);
    }

    [Benchmark, BenchmarkCategory("Scoped")]
    public async Task MediatR_Send_Void_Scoped()
    {
        using var scope = _mediatr.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IMediator>().Send(_mediatrVoid);
    }

    [Benchmark, BenchmarkCategory("Scoped")]
    public async Task<int> MediatR_Send_Result_Scoped()
    {
        using var scope = _mediatr.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(_mediatrInt);
    }

    [Benchmark, BenchmarkCategory("Scoped")]
    public async Task MediatR_Publish_Scoped()
    {
        using var scope = _mediatr.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IMediator>().Publish(_mediatrPing);
    }

    [Benchmark, BenchmarkCategory("Scoped")]
    public async Task MediatorSg_Send_Void_Scoped()
    {
        using var scope = _mediatorSg.CreateScope();
        await scope.ServiceProvider.GetRequiredService<Mediator.IMediator>().Send(_mediatorSgVoid);
    }

    [Benchmark, BenchmarkCategory("Scoped")]
    public async Task<int> MediatorSg_Send_Result_Scoped()
    {
        using var scope = _mediatorSg.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<Mediator.IMediator>().Send(_mediatorSgInt);
    }

    [Benchmark, BenchmarkCategory("Scoped")]
    public async Task MediatorSg_Publish_Scoped()
    {
        using var scope = _mediatorSg.CreateScope();
        await scope.ServiceProvider.GetRequiredService<Mediator.IMediator>().Publish(_mediatorSgPing);
    }
}
