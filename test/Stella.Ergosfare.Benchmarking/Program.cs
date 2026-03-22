using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Microsoft.Extensions.DependencyInjection;
using Stella.Ergosfare;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Core.Abstractions.Handlers;
using Stella.Ergosfare.Core.Abstractions.Registry;
using Stella.Ergosfare.Core.Abstractions.Strategies;
using Stella.Ergosfare.Core.Extensions.MicrosoftDependencyInjection;
using MediatR;
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
    private StellaMessage _stellaMessage;
    private MediateOptions<StellaMessage, Task> _stellaOptions;

    private IServiceProvider _mediatrProvider;
    private IMediator _mediatrMediator;
    private MediatrRequest _mediatrRequest;

    [GlobalSetup]
    public void Setup()
    {
        // Stella Setup
        var stellaServices = new ServiceCollection();
        stellaServices.AddErgosfare(options => {
            options.AddCoreModule(module => {
                module.Register<StellaHandler>();
            });
        });
        _stellaProvider = stellaServices.BuildServiceProvider();
        _stellaMediator = _stellaProvider.GetRequiredService<IMessageMediator>();
        _stellaMessage = new StellaMessage();

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
    }

    [Benchmark]
    public async Task StellaErgosfare()
    {
        
        for (var i = 0; i < 100000; i++) 
            await _stellaMediator.Mediate(_stellaMessage, _stellaOptions);
    }

    [Benchmark]
    public async Task MediatR()
    {
        for (var i = 0; i < 100000; i++) 
            await _mediatrMediator.Send(_mediatrRequest);
    }
}
