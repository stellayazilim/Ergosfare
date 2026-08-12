using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Attributes;
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

/// <summary>
/// Two scenarios, grouped as table categories:
/// <para><b>Root</b> — mediators resolved once from the root provider (singleton-style
/// usage: background workers, message-pump loops).</para>
/// <para><b>Scoped</b> — a fresh DI scope per dispatch (the web-request shape; includes
/// scope creation and mediator resolution in every measurement).</para>
/// Each category carries the raw <see cref="IMessageMediator"/> engine dispatch as its
/// baseline — that is the floor the public facades add their convenience on top of, and
/// the Ratio column reads as "what does the facade (or MediatR) cost relative to it".
/// Within each category: a void dispatch, a result-returning dispatch, and a two-handler
/// event publish, for Ergosfare and the competitors alike.
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

    private IMessageMediator _engine = null!;
    private MessageDispatchEngine _dispatchEngine = null!;
    private ICommandMediator _commands = null!;
    private ICommandMediator _generatedCommands = null!;
    private ICommandMediator _memoizedCommands = null!;
    private IQueryMediator _queries = null!;
    private IQueryMediator _generatedQueries = null!;
    private IEventMediator _events = null!;
    private IMediator _mediator = null!;
    private Mediator.IMediator _martinMediator = null!;

    private readonly VoidCommand _voidCommand = new();
    private readonly IntQuery _intQuery = new();
    private readonly PingEvent _pingEvent = new();
    private readonly GroupedCommand _groupedCommand = new();
    private readonly GroupedPingEvent _groupedPingEvent = new();
    private readonly InterceptedCommand _interceptedCommand = new();
    private readonly InterceptedIntQuery _interceptedIntQuery = new();

    private static readonly string[] BenchGroups = ["bench"];
    private static readonly GroupSet BenchGroupSet = GroupSet.Of("bench");

    // Reused across dispatches, mirroring a caller that keeps its settings: the grouped
    // rows measure the grouped lane itself, not per-call settings construction.
    private readonly CommandMediationSettings _groupedCommandSettings = new() { Filters = { Groups = BenchGroups } };
    private readonly EventMediationSettings _groupedEventSettings = new() { Filters = { Groups = BenchGroups } };
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
                });
                options.AddEventModule(events =>
                {
                    events.Register<FirstPingEventHandler>();
                    events.Register<SecondPingEventHandler>();
                    events.Register<FirstGroupedPingEventHandler>();
                    events.Register<SecondGroupedPingEventHandler>();
                });
            })
            .BuildServiceProvider();

        _engine = _ergosfare.GetRequiredService<IMessageMediator>();
        _dispatchEngine = _ergosfare.GetRequiredService<MessageDispatchEngine>();
        _commands = _ergosfare.GetRequiredService<ICommandMediator>();
        _queries = _ergosfare.GetRequiredService<IQueryMediator>();
        _events = _ergosfare.GetRequiredService<IEventMediator>();

        _mediatr = new ServiceCollection()
            .AddMediatR(configuration => configuration.RegisterServicesFromAssembly(typeof(MediationBenchmark).Assembly))
            .BuildServiceProvider();

        _mediator = _mediatr.GetRequiredService<IMediator>();

        _mediatorSg = new ServiceCollection()
            .AddMediator()
            .BuildServiceProvider();

        _martinMediator = _mediatorSg.GetRequiredService<Mediator.IMediator>();
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
        nameof(Command_Void_Intercepted_Generated), nameof(Query_Result_Intercepted_Generated)])]
    public void SetupGenerated()
    {
        _ergosfareGenerated = new ServiceCollection()
            .AddErgosfare(options =>
            {
                options.AddCommandModule(commands => commands.RegisterGenerated());
                options.AddQueryModule(queries => queries.RegisterGenerated());
            })
            .BuildServiceProvider();

        _generatedCommands = _ergosfareGenerated.GetRequiredService<ICommandMediator>();
        _generatedQueries = _ergosfareGenerated.GetRequiredService<IQueryMediator>();
    }

    [GlobalCleanup(Targets = [nameof(Command_Void_Generated), nameof(Query_Result_Generated),
        nameof(Command_Void_Intercepted_Generated), nameof(Query_Result_Intercepted_Generated)])]
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
    [GlobalSetup(Targets = [nameof(Command_Void_Memoized)])]
    public void SetupMemoized()
    {
        _ergosfareMemoized = new ServiceCollection()
            .AddErgosfare(options =>
            {
                options.ForceMemoizedHandlers();
                options.AddCommandModule(commands => commands.Register<VoidCommandHandler>());
            })
            .BuildServiceProvider();

        _memoizedCommands = _ergosfareMemoized.GetRequiredService<ICommandMediator>();
    }

    [GlobalCleanup(Targets = [nameof(Command_Void_Memoized)])]
    public void CleanupMemoized()
    {
        _ergosfareMemoized.Dispose();
    }

    // ------------------------------------------------------------------
    // Root — mediators resolved once, no per-dispatch scope
    // ------------------------------------------------------------------

    [Benchmark(Baseline = true), BenchmarkCategory("Root")]
    public ValueTask Engine_Void() => _engine.DispatchAsync(_voidCommand);

    /// <summary>
    /// Typed engine dispatch: the compile-time message type resolves the executor from a
    /// static-generic holder — no dictionary lookup on the hot path.
    /// </summary>
    [Benchmark, BenchmarkCategory("Root")]
    public ValueTask Engine_Void_Typed() => _dispatchEngine.DispatchVoidAsync(_voidCommand, _ergosfare);

    [Benchmark, BenchmarkCategory("Root")]
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

    [Benchmark, BenchmarkCategory("Root")]
    public ValueTask Command_Void_Grouped() => _commands.SendAsync(_groupedCommand, _groupedCommandSettings);

    /// <summary>
    /// The canonical-filter overload: no settings object, and the grouped executor
    /// lookup matches the reused <see cref="GroupSet"/> on a single reference check.
    /// </summary>
    [Benchmark, BenchmarkCategory("Root")]
    public ValueTask Command_Void_Grouped_GroupSet() => _commands.SendAsync(_groupedCommand, BenchGroupSet);

    [Benchmark, BenchmarkCategory("Root")]
    public ValueTask Event_Publish() => _events.PublishAsync(_pingEvent);

    [Benchmark, BenchmarkCategory("Root")]
    public ValueTask Event_Publish_Grouped() => _events.PublishAsync(_groupedPingEvent, _groupedEventSettings);

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
    public async Task Engine_Void_Scoped()
    {
        using var scope = _ergosfare.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IMessageMediator>().DispatchAsync(_voidCommand);
    }

    [Benchmark, BenchmarkCategory("Scoped")]
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
