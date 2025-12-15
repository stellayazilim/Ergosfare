using Stella.Ergosfare.Core.Abstractions.SignalHub.Signals;

namespace Ergosfare.Examples.PluginExample;

internal class ExampleService :  IDisposable
{
    private readonly IDisposable _subscription;

    public ExampleService()
    {
        // Örnek: BeginPipelineEvent'ina abone oluyoruz
        _subscription = PipelineSignal.Subscribe<BeginPipelineSignal>(OnPipelineEvent);
    }

    private void OnPipelineEvent(PipelineSignal evt)
    {
        Console.WriteLine($"[ExamplePlugin] Hello World! Event received: {evt.GetType().Name}");
    }

    public void Dispose()
    {
        _subscription.Dispose();
    }
}