using Stella.Ergosfare.Commands.Abstractions;
using Stella.Ergosfare.Core.Abstractions;
using Stella.Ergosfare.Events.Abstractions;
using Stella.Ergosfare.Queries.Abstractions;

namespace Stella.Ergosfare.Contract.Test.Harness;

/// <summary>
/// Hands a <see cref="PipelineRecorder"/> to a dispatch through the mediation settings'
/// <c>Items</c> dictionary and reads the stage marks back off the caller's own object —
/// the whole observation channel, built from public surface only.
/// </summary>
public static class Recording
{
    /// <summary>Settings that carry <paramref name="recorder"/> into a command dispatch.</summary>
    public static CommandMediationSettings Commands(this PipelineRecorder recorder)
        => new() { Items = { [PipelineRecorder.ItemsKey] = recorder } };

    /// <summary>Settings that carry <paramref name="recorder"/> into a query dispatch.</summary>
    public static QueryMediationSettings Queries(this PipelineRecorder recorder)
        => new() { Items = { [PipelineRecorder.ItemsKey] = recorder } };

    /// <summary>Settings that carry <paramref name="recorder"/> into a publish.</summary>
    public static EventMediationSettings Events(this PipelineRecorder recorder)
        => new() { Items = { [PipelineRecorder.ItemsKey] = recorder } };

    /// <summary>Shorthand for a pipeline participant marking its own stage.</summary>
    public static void Mark(this IExecutionContext context, string stage, string? detail = null)
        => PipelineRecorder.From(context).Mark(stage, detail);
}
