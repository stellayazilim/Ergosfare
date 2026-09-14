using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Generated;

using Stella.Ergosfare.Queries.Abstractions;
using Stella.Ergosfare.Queries.Extensions.MicrosoftDependencyInjection;

namespace Stella.Ergosfare.Benchmarking;

/// <summary>
/// What a dispatch costs the CPU, not just the clock: cache misses, branch
/// mispredictions and retired instructions for the two elementary shapes — a void command
/// and a result-bearing query — pinned to one core.
/// </summary>
/// <remarks>
/// <para>This is the core modernization's measurement gate. Nanoseconds and bytes already
/// have a home in <see cref="MediationBenchmark"/>; they cannot say whether a redesign
/// paid for its speed with instruction-cache footprint or an extra unpredictable branch,
/// which is exactly what a single-core deployment feels first. A baseline is captured
/// before the surgery and re-run after every large merge.</para>
/// <para>Compares explicit and generated registration using compiled plans against
/// MediatR and martinothamar/Mediator defaults. The generated registration rows use
/// a targeted setup in their own benchmark process.</para>
/// <para>Counter collection needs an elevated console on Windows; see
/// <see cref="CachePressureConfig"/> for what an unelevated run still reports.</para>
/// </remarks>
[MemoryDiagnoser]
[Config(typeof(CachePressureConfig))]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class CachePressureBenchmark
{
    private const string VoidShape = "Command_Void";
    private const string ResultShape = "Query_Result";

    private ServiceProvider _ergosfare = null!;
    private ServiceProvider _ergosfareGenerated = null!;
    private ServiceProvider _mediatr = null!;
    private ServiceProvider _mediatorSg = null!;

    private ICommandMediator _commands = null!;
    private IQueryMediator _queries = null!;
    private ICommandMediator _generatedCommands = null!;
    private IQueryMediator _generatedQueries = null!;
    private IMediator _mediator = null!;
    private Mediator.IMediator _martinMediator = null!;

    private readonly VoidCommand _voidCommand = new();
    private readonly IntQuery _intQuery = new();
    private readonly MediatrVoidRequest _mediatrVoid = new();
    private readonly MediatrIntRequest _mediatrInt = new();
    private readonly MediatorSgVoidCommand _mediatorSgVoid = new();
    private readonly MediatorSgIntQuery _mediatorSgInt = new();

    /// <summary>Builds the runtime-registered lane and both competitors.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _ergosfare = new ServiceCollection()
            .AddErgosfare(options =>
            {
                options.AddCommandModule(commands => commands.Register<VoidCommandHandler>());
                options.AddQueryModule(queries => queries.Register<IntQueryHandler>());
            })
            .BuildServiceProvider();

        _commands = _ergosfare.GetRequiredService<ICommandMediator>();
        _queries = _ergosfare.GetRequiredService<IQueryMediator>();

        _mediatr = new ServiceCollection()
            .AddMediatR(configuration =>
                configuration.RegisterServicesFromAssembly(typeof(CachePressureBenchmark).Assembly))
            .BuildServiceProvider();

        _mediator = _mediatr.GetRequiredService<IMediator>();

        _mediatorSg = new ServiceCollection()
            .AddMediator()
            .BuildServiceProvider();

        _martinMediator = _mediatorSg.GetRequiredService<Mediator.IMediator>();

        AssertQueryAnswers(() => _queries.QueryAsync(_intQuery).AsTask().GetAwaiter().GetResult(), "Ergosfare");
        AssertQueryAnswers(() => _mediator.Send(_mediatrInt).GetAwaiter().GetResult(), "MediatR");
        AssertQueryAnswers(() => _martinMediator.Send(_mediatorSgInt).AsTask().GetAwaiter().GetResult(), "Mediator");
    }

    /// <summary>Releases the providers built by <see cref="Setup"/>.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _ergosfare.Dispose();
        _mediatr.Dispose();
        _mediatorSg.Dispose();
    }

    /// <summary>Builds the source-generated lane in its own benchmark process.</summary>
    [GlobalSetup(Targets = [nameof(Command_Void_Generated), nameof(Query_Result_Generated),
        nameof(Query_Result_Generated_Typed)])]
    public void SetupGenerated()
    {
        _ergosfareGenerated = new ServiceCollection()
            .AddErgosfare(options =>
            {
                options.AddCommandModule(commands => commands.AddGenerated());
                options.AddQueryModule(queries => queries.AddGenerated());
            })
            .BuildServiceProvider();

        _generatedCommands = _ergosfareGenerated.GetRequiredService<ICommandMediator>();
        _generatedQueries = _ergosfareGenerated.GetRequiredService<IQueryMediator>();

        AssertQueryAnswers(
            () => _generatedQueries.QueryAsync(_intQuery).AsTask().GetAwaiter().GetResult(),
            "Ergosfare (generated)");
    }

    /// <summary>Releases the source-generated lane's provider.</summary>
    [GlobalCleanup(Targets = [nameof(Command_Void_Generated), nameof(Query_Result_Generated),
        nameof(Query_Result_Generated_Typed)])]
    public void CleanupGenerated() => _ergosfareGenerated.Dispose();

    // ------------------------------------------------------------------
    // Void command — nothing comes back; the shape every fast lane targets first
    // ------------------------------------------------------------------

    /// <summary>Runtime-registered handler, resolved transiently per dispatch.</summary>
    [Benchmark(Baseline = true), BenchmarkCategory(VoidShape)]
    public ValueTask Command_Void() => _commands.SendAsync(_voidCommand);

    /// <summary>The compile-time dispatch root and plan emitted by the generator.</summary>
    [Benchmark, BenchmarkCategory(VoidShape)]
    public ValueTask Command_Void_Generated() => _generatedCommands.SendAsync(_voidCommand);

    /// <summary>MediatR on its defaults: reflection-based dispatch, transient handlers.</summary>
    [Benchmark, BenchmarkCategory(VoidShape)]
    public Task MediatR_Send_Void() => _mediator.Send(_mediatrVoid);

    /// <summary>martinothamar/Mediator on its defaults: generated dispatch, singletons.</summary>
    [Benchmark, BenchmarkCategory(VoidShape)]
    public ValueTask<Mediator.Unit> MediatorSg_Send_Void() => _martinMediator.Send(_mediatorSgVoid);

    // ------------------------------------------------------------------
    // Result query — the same five lanes with a value travelling back
    // ------------------------------------------------------------------

    /// <inheritdoc cref="Command_Void" />
    [Benchmark(Baseline = true), BenchmarkCategory(ResultShape)]
    public ValueTask<int> Query_Result() => _queries.QueryAsync(_intQuery);

    /// <inheritdoc cref="Command_Void_Generated" />
    [Benchmark, BenchmarkCategory(ResultShape)]
    public ValueTask<int> Query_Result_Generated() => _generatedQueries.QueryAsync(_intQuery);

    /// <summary>
    /// The same generated lane reached through the typed overload: the query's own type is
    /// a type argument, so the executor is a static generic field read instead of a
    /// <c>GetType()</c>, a <c>(message, result)</c> tuple hash and a slot refresh. The pair
    /// against <see cref="Query_Result_Generated"/> is what that lookup costs.
    /// </summary>
    [Benchmark, BenchmarkCategory(ResultShape)]
    public ValueTask<int> Query_Result_Generated_Typed()
        => _generatedQueries.QueryAsync<IntQuery, int>(_intQuery);

    /// <inheritdoc cref="MediatR_Send_Void" />
    [Benchmark, BenchmarkCategory(ResultShape)]
    public Task<int> MediatR_Send_Result() => _mediator.Send(_mediatrInt);

    /// <inheritdoc cref="MediatorSg_Send_Void" />
    [Benchmark, BenchmarkCategory(ResultShape)]
    public ValueTask<int> MediatorSg_Send_Result() => _martinMediator.Send(_mediatorSgInt);

    /// <summary>
    /// A lane that silently stops dispatching still benchmarks beautifully. Every setup
    /// proves its provider answers before any measurement runs.
    /// </summary>
    private static void AssertQueryAnswers(Func<int> dispatch, string library)
    {
        var result = dispatch();

        if (result != 7)
        {
            throw new InvalidOperationException(
                $"{library} query returned {result}, expected 7 — the handler did not run.");
        }
    }
}
