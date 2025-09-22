using System.Collections.Generic;

namespace Ergosfare.Core.Abstractions.SignalHub.Signals;

public class PipelineRetrySignal(
    object message,
    object? result, 
    List<IPipelineCheckpoint>? checkpoints
    ): Signal
{
    internal IReadOnlyList<IPipelineCheckpoint> Checkpoints { get; } = checkpoints ?? [];
    internal TMessage GetMessage<TMessage>() => (TMessage)message;
    internal TResult? GetResult<TResult>() => (TResult?)result;
    
    public override IEnumerable<object> GetEqualityComponents()
    {
        yield return message;
        yield return result ?? new object();
        yield return Checkpoints;
    }
}