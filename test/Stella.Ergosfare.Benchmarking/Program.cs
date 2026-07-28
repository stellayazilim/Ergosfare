using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Events.Abstractions;
using Stella.Ergosfare.Events.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Generated;
using Stella.Ergosfare.Queries.Abstractions;
using Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection;
using MediatR;

namespace Stella.Ergosfare.Benchmarks;

public class Program
{
    public static void Main(string[] args)
    {
        BenchmarkRunner.Run<MediationBenchmark>();
    }
}

// ---------------------------------------------------------------------------
// Ergosfare messages & handlers
// ---------------------------------------------------------------------------

public sealed class VoidCommand : Stella.Ergosfare.Commands.Abstractions.ICommand { }

public sealed class VoidCommandHandler : ICommandHandler<VoidCommand>
{
    public ValueTask HandleAsync(VoidCommand command, IExecutionContext context) => ValueTask.CompletedTask;
}

public sealed class IntQuery : IQuery<int> { }

public sealed class IntQueryHandler : IQueryHandler<IntQuery, int>
{
    public ValueTask<int> HandleAsync(IntQuery query, IExecutionContext context) => ValueTask.FromResult(7);
}

public sealed class PingEvent : IEvent { }

public sealed class FirstPingEventHandler : IEventHandler<PingEvent>
{
    public ValueTask HandleAsync(PingEvent @event, IExecutionContext context) => ValueTask.CompletedTask;
}

public sealed class SecondPingEventHandler : IEventHandler<PingEvent>
{
    public ValueTask HandleAsync(PingEvent @event, IExecutionContext context) => ValueTask.CompletedTask;
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
/// event publish, for Ergosfare and MediatR alike.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class MediationBenchmark
{
    private ServiceProvider _ergosfare = null!;
    private ServiceProvider _ergosfareGenerated = null!;
    private ServiceProvider _mediatr = null!;

    private IMessageMediator _engine = null!;
    private ICommandMediator _commands = null!;
    private ICommandMediator _generatedCommands = null!;
    private IQueryMediator _queries = null!;
    private IEventMediator _events = null!;
    private IMediator _mediator = null!;

    private readonly VoidCommand _voidCommand = new();
    private readonly IntQuery _intQuery = new();
    private readonly PingEvent _pingEvent = new();
    private readonly MediatrVoidRequest _mediatrVoid = new();
    private readonly MediatrIntRequest _mediatrInt = new();
    private readonly MediatrPingNotification _mediatrPing = new();

    [GlobalSetup]
    public void Setup()
    {
        _ergosfare = new ServiceCollection()
            .AddErgosfare(options =>
            {
                options.AddCommandModule(commands => commands.Register<VoidCommandHandler>());
                options.AddQueryModule(queries => queries.Register<IntQueryHandler>());
                options.AddEventModule(events =>
                {
                    events.Register<FirstPingEventHandler>();
                    events.Register<SecondPingEventHandler>();
                });
            })
            .BuildServiceProvider();

        _engine = _ergosfare.GetRequiredService<IMessageMediator>();
        _commands = _ergosfare.GetRequiredService<ICommandMediator>();
        _queries = _ergosfare.GetRequiredService<IQueryMediator>();
        _events = _ergosfare.GetRequiredService<IEventMediator>();

        _mediatr = new ServiceCollection()
            .AddMediatR(configuration => configuration.RegisterServicesFromAssembly(typeof(MediationBenchmark).Assembly))
            .BuildServiceProvider();

        _mediator = _mediatr.GetRequiredService<IMediator>();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _ergosfare.Dispose();
        _mediatr.Dispose();
    }

    /// <summary>
    /// Source-generated registration variant, isolated to its own benchmark process via
    /// targets: <c>RegisterGenerated()</c> installs the compile-time dispatch roots and
    /// void pipeline plans process-wide, and the targeted setup keeps that installation
    /// away from the runtime-registration rows' processes so the comparison stays honest.
    /// </summary>
    [GlobalSetup(Targets = [nameof(Command_Void_Generated)])]
    public void SetupGenerated()
    {
        _ergosfareGenerated = new ServiceCollection()
            .AddErgosfare(options => options.AddCommandModule(commands => commands.RegisterGenerated()))
            .BuildServiceProvider();

        _generatedCommands = _ergosfareGenerated.GetRequiredService<ICommandMediator>();
    }

    [GlobalCleanup(Targets = [nameof(Command_Void_Generated)])]
    public void CleanupGenerated()
    {
        _ergosfareGenerated.Dispose();
    }

    // ------------------------------------------------------------------
    // Root — mediators resolved once, no per-dispatch scope
    // ------------------------------------------------------------------

    [Benchmark(Baseline = true), BenchmarkCategory("Root")]
    public ValueTask Engine_Void() => _engine.DispatchAsync(_voidCommand);

    [Benchmark, BenchmarkCategory("Root")]
    public ValueTask Command_Void() => _commands.SendAsync(_voidCommand);

    [Benchmark, BenchmarkCategory("Root")]
    public ValueTask Command_Void_Generated() => _generatedCommands.SendAsync(_voidCommand);

    [Benchmark, BenchmarkCategory("Root")]
    public ValueTask<int> Query_Result() => _queries.QueryAsync(_intQuery);

    [Benchmark, BenchmarkCategory("Root")]
    public ValueTask Event_Publish() => _events.PublishAsync(_pingEvent);

    [Benchmark, BenchmarkCategory("Root")]
    public Task MediatR_Send_Void() => _mediator.Send(_mediatrVoid);

    [Benchmark, BenchmarkCategory("Root")]
    public Task<int> MediatR_Send_Result() => _mediator.Send(_mediatrInt);

    [Benchmark, BenchmarkCategory("Root")]
    public Task MediatR_Publish() => _mediator.Publish(_mediatrPing);

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
}
