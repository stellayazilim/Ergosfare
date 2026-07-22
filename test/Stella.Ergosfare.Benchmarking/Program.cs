using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare;
using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Commands.Extensions.MicrosoftDependencyInjection;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Abstractions.Registry;
using Stella.Ergosfare.Core.Abstractions.Strategies;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using MediatR;
using LiteBus.Commands;
using LiteBus.Extensions.Microsoft.DependencyInjection;
using System.Threading.Tasks;
using System.Threading;

namespace Stella.Ergosfare.Benchmarks;

public class Program
{
    public static void Main(string[] args)
    {
        BenchmarkRunner.Run<MediationBenchmark>();
    }
}

public class StellaMessage : IMessage { }
public class StellaHandler : IAsyncHandler<StellaMessage>
{
    public Task HandleAsync(StellaMessage message, IExecutionContext context) => Task.CompletedTask;
}

public class StellaCommand : ICommand { }
public class StellaCommandHandler : ICommandHandler<StellaCommand>
{
    public Task HandleAsync(StellaCommand message, IExecutionContext context) => Task.CompletedTask;
}

public class LiteBusCommand : LiteBus.Commands.Abstractions.ICommand { }
public class LiteBusCommandHandler : LiteBus.Commands.Abstractions.ICommandHandler<LiteBusCommand>
{
    public Task HandleAsync(LiteBusCommand message, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public class MediatrRequest : IRequest { }
public class MediatrHandler : IRequestHandler<MediatrRequest>
{
    public Task Handle(MediatrRequest request, CancellationToken cancellationToken) => Task.CompletedTask;
}

[MemoryDiagnoser]
public class MediationBenchmark
{
    private IServiceProvider _stellaProvider;
    private IMessageMediator _stellaMediator;
    private ICommandMediator _stellaCommandMediator;
    private StellaMessage _stellaMessage;
    private StellaCommand _stellaCommand;
    private MediateOptions<StellaMessage, Task> _stellaOptions;

    private IServiceProvider _mediatrProvider;
    private IMediator _mediatrMediator;
    private MediatrRequest _mediatrRequest;

    private IServiceProvider _liteBusProvider;
    private LiteBus.Commands.Abstractions.ICommandMediator _liteBusCommandMediator;
    private LiteBusCommand _liteBusCommand;

    [GlobalSetup]
    public void Setup()
    {
        // Stella Setup
        var stellaServices = new ServiceCollection();
        stellaServices.AddErgosfare(options => {
            options.AddCoreModule(module => {
                module.Register<StellaHandler>();
            });
            options.AddCommandModule(module => {
                module.Register<StellaCommandHandler>();
            });
        });
        _stellaProvider = stellaServices.BuildServiceProvider();
        _stellaMediator = _stellaProvider.GetRequiredService<IMessageMediator>();
        _stellaCommandMediator = _stellaProvider.GetRequiredService<ICommandMediator>();
        _stellaMessage = new StellaMessage();
        _stellaCommand = new StellaCommand();

        _stellaOptions = new MediateOptions<StellaMessage, Task>
        {
            CancellationToken = CancellationToken.None,
            Groups = [],
            MessageMediationStrategy = new SingleAsyncHandlerMediationStrategy<StellaMessage>(null),
            MessageResolveStrategy = new ActualTypeOrFirstAssignableTypeMessageResolveStrategy(_stellaProvider.GetRequiredService<IMessageRegistry>())
        };

        // MediatR Setup
        var mediatrServices = new ServiceCollection();
        mediatrServices.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(MediationBenchmark).Assembly));
        _mediatrProvider = mediatrServices.BuildServiceProvider();
        _mediatrMediator = _mediatrProvider.GetRequiredService<IMediator>();
        _mediatrRequest = new MediatrRequest();

        // LiteBus Setup
        var liteBusServices = new ServiceCollection();
        liteBusServices.AddLiteBus(builder => {
            builder.AddCommandModule(module => {
                module.Register<LiteBusCommandHandler>();
            });
        });
        _liteBusProvider = liteBusServices.BuildServiceProvider();
        _liteBusCommandMediator = _liteBusProvider.GetRequiredService<LiteBus.Commands.Abstractions.ICommandMediator>();
        _liteBusCommand = new LiteBusCommand();
    }

    [Benchmark]
    public async Task StellaErgosfare()
    {

        for (var i = 0; i < 100000; i++)
            await _stellaMediator.Mediate(_stellaMessage, _stellaOptions);
    }

    // Full public API path (settings/options/strategy handling included),
    // symmetric with the MediatR benchmark which also uses its public Send.
    [Benchmark]
    public async Task StellaErgosfare_PublicApi()
    {
        for (var i = 0; i < 100000; i++)
            await _stellaCommandMediator.SendAsync(_stellaCommand);
    }

    [Benchmark]
    public async Task MediatR()
    {
        for (var i = 0; i < 100000; i++)
            await _mediatrMediator.Send(_mediatrRequest);
    }

    // Ergosfare is an independent implementation whose design (API surface, naming) was
    // heavily inspired by LiteBus, so LiteBus is included as a reference point.
    [Benchmark]
    public async Task LiteBus_PublicApi()
    {
        for (var i = 0; i < 100000; i++)
            await _liteBusCommandMediator.SendAsync(_liteBusCommand);
    }

    // Fresh DI scope per dispatch — the realistic per-request shape where
    // lifetime-aware (scoped) handler resolution actually pays its cost.
    [Benchmark]
    public async Task StellaErgosfare_PublicApi_ScopePerDispatch()
    {
        for (var i = 0; i < 100000; i++)
        {
            using var scope = _stellaProvider.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ICommandMediator>().SendAsync(_stellaCommand);
        }
    }

    [Benchmark]
    public async Task MediatR_ScopePerDispatch()
    {
        for (var i = 0; i < 100000; i++)
        {
            using var scope = _mediatrProvider.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IMediator>().Send(_mediatrRequest);
        }
    }
}
