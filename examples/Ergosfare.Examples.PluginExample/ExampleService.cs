using Ergosfare.Core.Abstractions.Events;

namespace Ergosfare.Examples.PluginExample;

internal class ExampleService :  IDisposable
{
    private readonly IDisposable _subscription;

    public ExampleService()
    {
        // Örnek: BeginPipelineEvent'ina abone oluyoruz
        _subscription = PipelineEvent.Subscribe<BeginPipelineEvent>(OnPipelineEvent);
    }

    private void OnPipelineEvent(PipelineEvent evt)
    {
        Console.WriteLine($"[ExamplePlugin] Hello World! Event received: {evt.GetType().Name}");
    }

    public void Dispose()
    {
        _subscription.Dispose();
    }
}