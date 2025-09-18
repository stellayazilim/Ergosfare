using System;
using System.Collections.Generic;
using System.Linq;

namespace Ergosfare.Core.Abstractions.SignalHub.Signals;

public sealed class FinishExceptionInterceptingSignal: PipelineSignal
{
    public required Exception Exception { get; init; }
    
    public static FinishExceptionInterceptingSignal Create(object message, object? result, Exception exception) => new()
    {
        Message = message ?? throw new ArgumentNullException(nameof(message)),
        Result = result,
        Exception = exception ?? throw new ArgumentNullException(nameof(exception))
    };
    
    public static void Invoke(object message, object? result, Exception exception) =>
        SignalHubAccessor.Instance.Publish(Create(message, result, exception));
    
    public override IEnumerable<object> GetEqualityComponents()
        => base.GetEqualityComponents().Concat([Exception]);
}